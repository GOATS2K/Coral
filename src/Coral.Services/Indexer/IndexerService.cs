using System.Text.RegularExpressions;
using AutoMapper;
using Coral.BulkExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coral.Services.Indexer;

public interface IIndexerService
{
    Task DeleteTrack(string filePath);
    Task HandleRename(string oldPath, string newPath);
    IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<Indexer.DirectoryGroup> directoryGroups,
        MusicLibrary library,
        CancellationToken cancellationToken = default);
    Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default);
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<IndexerService> _logger;
    private readonly IEmbeddingChannel _embeddingChannel;

    private static readonly string[] AudioFileFormats =
        [".flac", ".mp3", ".mp2", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus"];

    private static readonly string[] ImageFileFormats = [".jpg", ".png"];

    public IndexerService(
        CoralDbContext context,
        ISearchService searchService,
        ILogger<IndexerService> logger,
        IArtworkService artworkService,
        IEmbeddingChannel embeddingChannel)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
        _embeddingChannel = embeddingChannel;
    }

    #region Public API Methods

    public async Task DeleteTrack(string filePath)
    {
        var track = await _context.Tracks
            .Include(t => t.AudioFile)
            .FirstOrDefaultAsync(t => t.AudioFile.FilePath == filePath);

        if (track == null)
        {
            _logger.LogWarning("Track {FilePath} not found", filePath);
            return;
        }

        _context.Tracks.Remove(track);
        await _context.SaveChangesAsync();
        await DeleteEmptyArtistsAndAlbums();
    }

    public async Task HandleRename(string oldPath, string newPath)
    {
        var audioFile = await _context.AudioFiles.FirstOrDefaultAsync(af => af.FilePath == oldPath);
        if (audioFile == null)
        {
            _logger.LogWarning("Audio file {OldPath} not found", oldPath);
            return;
        }

        audioFile.FilePath = newPath;
        audioFile.UpdatedAt = File.GetLastWriteTimeUtc(newPath);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Core Indexing Logic - Bulk Operations

    private readonly List<Track> _tracksForKeywordInsertion = new();
    private readonly Dictionary<Album, string> _albumsForArtworkProcessing = new();

    private async Task IndexDirectory(List<FileInfo> tracksInDirectory, MusicLibrary library)
    {
        var analyzedTracks = await ReadTracksInDirectory(tracksInDirectory);
        bool folderIsAlbum = analyzedTracks
            .Select(x => x.Album)
            .Distinct().Count() == 1;

        var parent = Directory.GetParent(tracksInDirectory.First().FullName)?.FullName;

        if (folderIsAlbum)
        {
            _logger.LogDebug("Indexing {Path} as album", parent);
            await IndexAlbumBulk(analyzedTracks, library);
        }
        else
        {
            _logger.LogDebug("Indexing {Path} as single files", parent);
            await IndexSingleFilesBulk(analyzedTracks, library);
        }
    }

    private async Task IndexAlbumBulk(List<ATL.Track> tracks, MusicLibrary library)
    {
        // Parse all artists for all tracks (deduplicated automatically by BulkInsertContext caching)
        var artistsForTracks = new Dictionary<ATL.Track, List<ArtistWithRole>>();
        foreach (var track in tracks)
        {
            var artists = await ParseArtistsBulk(track.Artist, track.Title);
            artistsForTracks.Add(track, artists);
        }

        // Collect all unique album artists from all tracks (for various artist releases)
        var allArtistsWithRoles = artistsForTracks.Values.SelectMany(a => a).ToList();
        var albumArtists = allArtistsWithRoles
            .GroupBy(a => a.ArtistId)
            .Select(g => g.First()) // Take first occurrence of each unique artist
            .ToList();

        // Determine album type
        var albumType = AlbumTypeHelper.GetAlbumType(
            albumArtists.Count(a => a.Role == ArtistRole.Main),
            tracks.Count);

        // Get or create album with bulk caching
        var firstTrack = tracks.First();
        var album = await GetAlbumBulk(albumArtists, firstTrack);
        album.Type = albumType;

        // Index each track
        foreach (var trackToIndex in tracks)
        {
            var trackGenre = !string.IsNullOrEmpty(trackToIndex.Genre)
                ? await GetGenreBulk(trackToIndex.Genre)
                : null;

            await IndexTrackBulk(artistsForTracks[trackToIndex], album, trackGenre, trackToIndex, library);
        }
    }

    private async Task IndexSingleFilesBulk(List<ATL.Track> tracks, MusicLibrary library)
    {
        foreach (var track in tracks)
        {
            var artists = await ParseArtistsBulk(track.Artist, track.Title);
            var genre = !string.IsNullOrEmpty(track.Genre)
                ? await GetGenreBulk(track.Genre)
                : null;

            // Create single-track album
            var album = await GetAlbumBulk(artists, track);

            await IndexTrackBulk(artists, album, genre, track, library);
        }
    }

    private async Task IndexTrackBulk(
        List<ArtistWithRole> artists,
        Album album,
        Genre? genre,
        ATL.Track atlTrack,
        MusicLibrary library)
    {
        // Check if track already exists (for rescans)
        var existingTrack = await _context.Tracks
            .Include(t => t.AudioFile)
            .FirstOrDefaultAsync(t => t.AudioFile!.FilePath == atlTrack.Path);

        if (existingTrack != null)
        {
            await UpdateTrackBulk(existingTrack, artists, album, genre, atlTrack);
        }
        else
        {
            await CreateTrackBulk(artists, album, genre, atlTrack, library);
        }
    }

    private async Task CreateTrackBulk(
        List<ArtistWithRole> artists,
        Album album,
        Genre? genre,
        ATL.Track atlTrack,
        MusicLibrary library)
    {
        // Defer artwork processing until after bulk save completes
        if (!_albumsForArtworkProcessing.ContainsKey(album))
        {
            var albumArtwork = await GetAlbumArtwork(atlTrack);
            if (albumArtwork != null)
            {
                _albumsForArtworkProcessing[album] = albumArtwork;
            }
        }

        // Get or create audio metadata (cached automatically)
        var audioMetadata = await _context.AudioMetadata.GetOrAddBulk(
            am => new { am.Codec, am.Bitrate, am.SampleRate },
            () => new AudioMetadata
            {
                Id = Guid.NewGuid(),
                Codec = atlTrack.AudioFormat.ShortName,
                Bitrate = atlTrack.Bitrate,
                SampleRate = atlTrack.SampleRate,
                Channels = atlTrack.ChannelsArrangement?.NbChannels,
                BitDepth = atlTrack.BitDepth != -1 ? atlTrack.BitDepth : null,
                CreatedAt = DateTime.UtcNow
            });

        // Create new audio file using bulk extensions
        var audioFile = await _context.AudioFiles.GetOrAddBulk(
            af => af.FilePath,
            () => new AudioFile
            {
                Id = Guid.NewGuid(),
                FilePath = atlTrack.Path,
                UpdatedAt = File.GetLastWriteTimeUtc(atlTrack.Path),
                FileSizeInBytes = new FileInfo(atlTrack.Path).Length,
                AudioMetadata = audioMetadata,
                AudioMetadataId = audioMetadata.Id,
                Library = library,
                LibraryId = library.Id,
                CreatedAt = DateTime.UtcNow
            });

        // Create new track using bulk extensions
        var track = await _context.Tracks.GetOrAddBulk(
            t => t.AudioFile!.FilePath,
            () => new Track
            {
                Id = Guid.NewGuid(),
                Title = !string.IsNullOrEmpty(atlTrack.Title) ? atlTrack.Title : Path.GetFileName(atlTrack.Path),
                Comment = atlTrack.Comment,
                Genre = genre,
                GenreId = genre?.Id,
                DiscNumber = atlTrack.DiscNumber,
                TrackNumber = atlTrack.TrackNumber,
                DurationInSeconds = atlTrack.Duration,
                Isrc = atlTrack.ISRC,
                Album = album,
                AlbumId = album.Id,
                AudioFile = audioFile,
                AudioFileId = audioFile.Id,
                Keywords = new List<Keyword>(),
                CreatedAt = DateTime.UtcNow
            });

        // Register track-artist relationships for new tracks
        foreach (var artistWithRole in artists)
        {
            _context.AddRelationshipBulk(track, artistWithRole);
        }

        // Defer keyword insertion until after SaveBulkChangesAsync (needs Artists navigation property populated)
        _tracksForKeywordInsertion.Add(track);

        // Queue for embedding extraction (async operation)
        await _embeddingChannel.GetWriter().WriteAsync(new EmbeddingJob(track, null));

        _logger.LogDebug("Created new track: {TrackPath}", atlTrack.Path);
    }

    private async Task UpdateTrackBulk(
        Track existingTrack,
        List<ArtistWithRole> newArtists,
        Album newAlbum,
        Genre? newGenre,
        ATL.Track atlTrack)
    {
        // Load existing relationships for comparison
        await _context.Entry(existingTrack).Collection(t => t.Artists).LoadAsync();

        // Check if artists changed by comparing IDs and roles
        var existingArtistIds = existingTrack.Artists
            .OrderBy(a => a.ArtistId)
            .ThenBy(a => a.Role)
            .Select(a => (a.ArtistId, a.Role))
            .ToList();
        var newArtistIds = newArtists
            .OrderBy(a => a.ArtistId)
            .ThenBy(a => a.Role)
            .Select(a => (a.ArtistId, a.Role))
            .ToList();
        var artistsChanged = !existingArtistIds.SequenceEqual(newArtistIds);

        // If album, genre, or artists changed, we need to persist the bulk cache first
        // because the new entities might only exist in the bulk cache, not in the database
        if (existingTrack.AlbumId != newAlbum.Id || existingTrack.GenreId != newGenre?.Id || artistsChanged)
        {
            await _context.SaveBulkChangesAsync();
        }

        // Update existing audio file through EF Core (scalar properties only)
        var audioFile = existingTrack.AudioFile!;
        var currentFileInfo = new FileInfo(atlTrack.Path);
        audioFile.UpdatedAt = currentFileInfo.LastWriteTimeUtc;
        audioFile.FileSizeInBytes = currentFileInfo.Length;

        // Update existing track scalar properties
        existingTrack.Title = !string.IsNullOrEmpty(atlTrack.Title) ? atlTrack.Title : Path.GetFileName(atlTrack.Path);
        existingTrack.Comment = atlTrack.Comment;
        existingTrack.DiscNumber = atlTrack.DiscNumber;
        existingTrack.TrackNumber = atlTrack.TrackNumber;
        existingTrack.DurationInSeconds = atlTrack.Duration;
        existingTrack.Isrc = atlTrack.ISRC;

        // Update album if changed
        if (existingTrack.AlbumId != newAlbum.Id)
        {
            existingTrack.Album = newAlbum;
            existingTrack.AlbumId = newAlbum.Id;
            _logger.LogDebug("Track {TrackPath} album changed to {NewAlbum}",
                atlTrack.Path, newAlbum.Name);
        }

        // Update genre if changed
        if (existingTrack.GenreId != newGenre?.Id)
        {
            existingTrack.Genre = newGenre;
            existingTrack.GenreId = newGenre?.Id;
            _logger.LogDebug("Track {TrackPath} genre changed to {NewGenre}",
                atlTrack.Path, newGenre?.Name ?? "(none)");
        }

        // Update artist relationships if changed
        if (artistsChanged)
        {
            // Clear existing artist relationships (EF Core will handle the junction table)
            existingTrack.Artists.Clear();

            // Add new artist relationships
            foreach (var artistWithRole in newArtists)
            {
                existingTrack.Artists.Add(artistWithRole);
            }

            _logger.LogDebug("Track {TrackPath} artists changed", atlTrack.Path);
        }

        // Save updates immediately via EF Core (don't mix with bulk operations)
        await _context.SaveChangesAsync();

        // Note: We don't call DeleteEmptyArtistsAndAlbums() here because:
        // - If only one track changes, old entities might not be empty yet
        // - Cleanup happens once at the end in FinalizeIndexing after all tracks are processed

        // Queue for embedding extraction (async operation)
        await _embeddingChannel.GetWriter().WriteAsync(new EmbeddingJob(existingTrack, null));

        _logger.LogDebug("Updated existing track: {TrackPath}", atlTrack.Path);
    }

    #endregion

    #region Get Or Add Methods - Using Bulk Extensions

    private async Task<Genre> GetGenreBulk(string genreName)
    {
        return await _context.Genres.GetOrAddBulk(
            keySelector: g => g.Name,
            createFunc: () => new Genre
            {
                Id = Guid.NewGuid(),
                Name = genreName,
                CreatedAt = DateTime.UtcNow
            });
    }

    private async Task<Artist> GetArtistBulk(string artistName)
    {
        if (string.IsNullOrEmpty(artistName)) artistName = "Unknown Artist";

        return await _context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () =>
            {
                _logger.LogDebug("Creating new artist: {Artist}", artistName);
                return new Artist
                {
                    Id = Guid.NewGuid(),
                    Name = artistName,
                    CreatedAt = DateTime.UtcNow
                };
            });
    }

    private async Task<List<ArtistWithRole>> GetArtistWithRoleBulk(List<string> artists, ArtistRole role)
    {
        var artistsWithRoles = new List<ArtistWithRole>();
        foreach (var artistName in artists)
        {
            var artist = await GetArtistBulk(artistName.Trim());

            var artistWithRole = await _context.ArtistsWithRoles.GetOrAddBulk(
                keySelector: awr => new { awr.ArtistId, awr.Role },
                createFunc: () => new ArtistWithRole
                {
                    Id = Guid.NewGuid(),
                    Artist = artist,
                    ArtistId = artist.Id,
                    Role = role,
                    CreatedAt = DateTime.UtcNow
                });

            artistsWithRoles.Add(artistWithRole);
        }

        return artistsWithRoles;
    }

    private async Task<List<ArtistWithRole>> ParseArtistsBulk(string artist, string title)
    {
        var parsedFeaturingArtists = ParseFeaturingArtists(title);
        var parsedRemixers = ParseRemixers(title);

        var guestArtists = !string.IsNullOrEmpty(parsedFeaturingArtists)
            ? await GetArtistWithRoleBulk(SplitArtist(parsedFeaturingArtists), ArtistRole.Guest)
            : new List<ArtistWithRole>();

        var remixers = !string.IsNullOrEmpty(parsedRemixers)
            ? await GetArtistWithRoleBulk(SplitArtist(parsedRemixers), ArtistRole.Remixer)
            : new List<ArtistWithRole>();

        var mainArtists = await GetArtistWithRoleBulk(SplitArtist(artist), ArtistRole.Main);

        var artistList = new List<ArtistWithRole>();
        artistList.AddRange(guestArtists);
        artistList.AddRange(mainArtists);
        artistList.AddRange(remixers);

        return artistList;
    }

    private async Task<RecordLabel?> GetRecordLabelBulk(ATL.Track atlTrack)
    {
        var trackHasLabel = atlTrack.AdditionalFields.TryGetValue("LABEL", out var labelName);
        if (!trackHasLabel) return null;

        return await _context.RecordLabels.GetOrAddBulk(
            keySelector: l => l.Name,
            createFunc: () => new RecordLabel
            {
                Id = Guid.NewGuid(),
                Name = labelName!,
                CreatedAt = DateTime.UtcNow
            });
    }

    private async Task<Album> GetAlbumBulk(List<ArtistWithRole> artists, ATL.Track atlTrack)
    {
        var albumName = !string.IsNullOrEmpty(atlTrack.Album)
            ? atlTrack.Album
            : Directory.GetParent(atlTrack.Path)?.Name;

        var label = await GetRecordLabelBulk(atlTrack);

        // Use composite key for album uniqueness
        var album = await _context.Albums.GetOrAddBulk(
            keySelector: a => new
            {
                a.Name,
                a.ReleaseYear,
                a.DiscTotal,
                a.TrackTotal
            },
            createFunc: () => new Album
            {
                Id = Guid.NewGuid(),
                Name = albumName!,
                ReleaseYear = atlTrack.Year,
                DiscTotal = atlTrack.DiscTotal,
                TrackTotal = atlTrack.TrackTotal,
                CatalogNumber = atlTrack.CatalogNumber,
                Label = label,
                Artworks = new List<Artwork>(),
                CreatedAt = DateTime.UtcNow
            });

        // Register album-artist relationships if not already present
        foreach (var artistWithRole in artists)
        {
            _context.AddRelationshipBulk(album, artistWithRole);
        }

        return album;
    }

    #endregion

    #region Utility Methods (No DB Access)

    private List<string> SplitArtist(string? artistName)
    {
        if (artistName == null) return new List<string>();
        string[] splitChars = { ",", "&", ";", " x " };
        var split = artistName.Split(splitChars, StringSplitOptions.TrimEntries);
        return split.Distinct().ToList();
    }

    private static string? ParseRemixers(string title)
    {
        // supports both (artist remix) and [artist remix]
        var pattern = @"\(([^()]*)(?: Edit| Remix| VIP| Bootleg)\)|\[([^[\[\]]*)(?: Edit| Remix| VIP| Bootleg)\]";
        var expression = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var remixerMatch = expression.Match(title).Groups.Values.Where(a => !string.IsNullOrEmpty(a.Value));
        var parsedRemixers = remixerMatch.LastOrDefault()?.Value.Trim();
        return parsedRemixers;
    }

    private static string? ParseFeaturingArtists(string title)
    {
        var pattern = @"\([fF](?:ea)?t(?:uring)?\.? (.*?)\)";
        var expression = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var featuringMatch = expression.Match(title);
        var parsedFeaturingArtists = featuringMatch.Groups.Values.LastOrDefault()?.Value.Trim();
        return parsedFeaturingArtists;
    }

    private async Task<string?> GetAlbumArtwork(ATL.Track atlTrack)
    {
        var albumDirectory = new DirectoryInfo(atlTrack.Path).Parent;

        var artwork = albumDirectory?.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => ImageFileFormats.Contains(Path.GetExtension(f.FullName)));

        return artwork?.FullName ?? await _artworkService.ExtractEmbeddedArtwork(atlTrack);
    }

    #endregion

    #region Scanning and Cleanup Methods

    private async Task ProcessArtworksBulkAsync(Dictionary<Album, string> albumsWithArtwork)
    {
        if (!albumsWithArtwork.Any()) return;

        // Reload albums from database (now that bulk save completed)
        var albumIds = albumsWithArtwork.Keys.Select(a => a.Id).ToList();
        var reloadedAlbums = await _context.Albums
            .Include(a => a.Artworks)
            .Where(a => albumIds.Contains(a.Id))
            .ToListAsync();

        // Build dictionary mapping reloaded albums to their artwork paths
        var reloadedAlbumsWithArtwork = new Dictionary<Album, string>();
        foreach (var reloadedAlbum in reloadedAlbums)
        {
            var originalAlbum = albumsWithArtwork.Keys.First(a => a.Id == reloadedAlbum.Id);
            var artworkPath = albumsWithArtwork[originalAlbum];
            reloadedAlbumsWithArtwork[reloadedAlbum] = artworkPath;
        }

        // Process all artworks in bulk
        await _artworkService.ProcessArtworksBulk(reloadedAlbumsWithArtwork);
    }

    private async Task InsertKeywordsForTracksBulkAsync(List<Track> tracks)
    {
        if (!tracks.Any()) return;

        // Reload tracks with navigation properties populated
        var trackIds = tracks.Select(t => t.Id).ToList();
        var reloadedTracks = await _context.Tracks
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Include(t => t.Album)
            .ThenInclude(a => a.Label)
            .Include(t => t.Keywords)
            .Where(t => trackIds.Contains(t.Id))
            .ToListAsync();

        // Insert keywords for all tracks in bulk
        await _searchService.InsertKeywordsForTracksBulk(reloadedTracks);
    }

    private async Task DeleteMissingTracks(MusicLibrary library)
    {
        var indexedFiles = _context.AudioFiles
            .Where(f => f.Library.Id == library.Id)
            .AsEnumerable();

        var missingFiles = indexedFiles
            .Where(f => !Path.Exists(f.FilePath))
            .Select(f => f.Id);

        var deletedTracks = await _context.Tracks
            .Where(f => missingFiles.Contains(f.AudioFile.Id))
            .ExecuteDeleteAsync();

        if (deletedTracks > 0)
        {
            _logger.LogInformation("Deleted {Tracks} missing tracks", deletedTracks);
        }

        await DeleteEmptyArtistsAndAlbums();

        // Finally, delete the missing file entries
        await _context.AudioFiles
            .Where(f => missingFiles.Contains(f.Id))
            .ExecuteDeleteAsync();
    }

    private async Task DeleteEmptyArtistsAndAlbums()
    {
        // Delete ArtistsWithRoles junction entries with no tracks
        var deletedArtistRoles = await _context.ArtistsWithRoles
            .Where(a => !a.Tracks.Any())
            .ExecuteDeleteAsync();

        if (deletedArtistRoles > 0)
        {
            _logger.LogInformation("Deleted {DeletedArtistRoles} artist roles with no tracks", deletedArtistRoles);
        }

        // Delete Artists with no roles (orphaned after relationship cleanup)
        var deletedArtists = await _context.Artists
            .Where(a => !_context.ArtistsWithRoles.Any(awr => awr.ArtistId == a.Id))
            .ExecuteDeleteAsync();

        if (deletedArtists > 0)
        {
            _logger.LogInformation("Deleted {DeletedArtists} orphaned artists", deletedArtists);
        }

        var emptyAlbumsArtwork = await _context.Albums
            .Include(t => t.Artworks)
            .Where(a => !a.Tracks.Any())
            .Select(a => a.Artworks)
            .SelectMany(x => x)
            .ToListAsync();

        foreach (var artwork in emptyAlbumsArtwork)
        {
            await _artworkService.DeleteArtwork(artwork);
        }

        var deletedAlbums = await _context.Albums
            .Where(a => !a.Tracks.Any())
            .ExecuteDeleteAsync();

        if (deletedAlbums > 0)
        {
            _logger.LogInformation("Deleted {DeletedAlbums} albums with no tracks", deletedAlbums);
        }
    }

    private async Task<MusicLibrary> GetLibrary(MusicLibrary library)
    {
        var existingLibrary = await _context.MusicLibraries.FindAsync(library.Id);
        return existingLibrary ?? library;
    }

    private async Task<List<ATL.Track>> ReadTracksInDirectory(List<FileInfo> tracksInDirectory)
    {
        var maxRetries = 10;
        var analyzedTracks = new List<ATL.Track>();

        foreach (var file in tracksInDirectory)
        {
            var retries = 0;
            while (retries < maxRetries)
            {
                try
                {
                    var track = new ATL.Track(file.FullName);
                    analyzedTracks.Add(track);
                    break;
                }
                catch (IOException ex)
                {
                    retries++;
                    _logger.LogWarning("Failed to read track {File} (attempt {Retry}/{Max}): {Error}",
                        file.FullName, retries, maxRetries, ex.Message);

                    if (retries >= maxRetries)
                    {
                        _logger.LogError("Failed to read track {File} after {Max} attempts", file.FullName, maxRetries);
                    }
                    else
                    {
                        await Task.Delay(100 * retries); // Exponential backoff
                    }
                }
            }
        }

        return analyzedTracks;
    }

    #endregion

    #region Streaming API for Benchmarks

    /// <summary>
    /// Indexes directory groups and yields tracks as they are indexed (streaming).
    /// Used for progress reporting in benchmarks.
    /// </summary>
    public async IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<Indexer.DirectoryGroup> directoryGroups,
        MusicLibrary library,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        library = await GetLibrary(library);

        await DeleteMissingTracks(library);

        await foreach (var directoryGroup in directoryGroups.WithCancellation(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            var filesInDirectory = directoryGroup.Files;
            if (!filesInDirectory.Any())
            {
                _logger.LogWarning("Skipping empty directory {Directory}", directoryGroup.DirectoryPath);
                continue;
            }

            // Track how many tracks we had before indexing this directory
            var tracksBeforeIndexing = _tracksForKeywordInsertion.Count;

            // Index the directory (stores in bulk context, doesn't save yet)
            await IndexDirectory(filesInDirectory, library);

            // Yield only the NEW tracks added by this directory
            var newTracksCount = _tracksForKeywordInsertion.Count - tracksBeforeIndexing;
            for (int i = 0; i < newTracksCount; i++)
            {
                yield return _tracksForKeywordInsertion[tracksBeforeIndexing + i];
            }

            _logger.LogDebug("Completed indexing of {Path}", directoryGroup.DirectoryPath);
        }
    }

    /// <summary>
    /// Finalizes indexing by saving all pending bulk operations and processing artworks/keywords.
    /// Must be called after IndexDirectoryGroups completes.
    /// </summary>
    public async Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        // Save all bulk operations at once
        var stats = await _context.SaveBulkChangesAsync(
            new BulkInsertOptions { Logger = _logger }, cancellationToken);

        _logger.LogInformation(
            "Bulk saved {Entities} entities and {Relationships} relationships in {Time:F2}s ({Rate:N0} entities/sec)",
            stats.TotalEntitiesInserted,
            stats.TotalRelationshipsInserted,
            stats.TotalTime.TotalSeconds,
            stats.TotalEntitiesInserted / stats.TotalTime.TotalSeconds);

        // Process artworks for albums using bulk method (now that bulk save completed and albums exist in DB)
        await ProcessArtworksBulkAsync(_albumsForArtworkProcessing);
        _albumsForArtworkProcessing.Clear();

        // Clear change tracker before keyword insertion to avoid stale entity conflicts
        _context.ChangeTracker.Clear();

        // Insert keywords for all saved tracks using bulk method (now that navigation properties are populated)
        await InsertKeywordsForTracksBulkAsync(_tracksForKeywordInsertion);
        _tracksForKeywordInsertion.Clear();

        // Clear change tracker before cleanup to ensure fresh queries
        _context.ChangeTracker.Clear();

        // Clean up orphaned albums and artists (from album/genre changes during rescans)
        await DeleteEmptyArtistsAndAlbums();

        _logger.LogInformation("Finalized indexing for {Directory}", library.LibraryPath);
        library.LastScan = DateTime.UtcNow;

        // Save library metadata
        await _context.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
