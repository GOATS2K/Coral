using System.Text.RegularExpressions;
using Coral.BulkExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coral.Services.Indexer;

public interface IIndexerService
{
    Task DeleteTrack(string filePath);
    Task HandleRename(string oldPath, string newPath);

    IAsyncEnumerable<IndexEvent> IndexDirectoryGroups(
        IAsyncEnumerable<DirectoryGroup> directoryGroups,
        MusicLibrary library,
        CancellationToken cancellationToken = default);

    Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default);
}

public enum IndexerOperation
{
    Create, Update, Delete
}

public record IndexEvent(IndexerOperation Operation, string FilePath, Track? Track);

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<IndexerService> _logger;

    private static readonly string[] ImageFileFormats = [".jpg", ".png"];

    private static readonly Regex RemixerParsingRegex = RegexPatterns.RemixerParsing();
    private static readonly Regex FeaturingArtistParsingRegex = RegexPatterns.FeaturingArtistParsing();

    public IndexerService(
        CoralDbContext context,
        ISearchService searchService,
        ILogger<IndexerService> logger,
        IArtworkService artworkService,
        IEmbeddingService embeddingService)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
        _embeddingService = embeddingService;
    }


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


    private readonly Dictionary<Track, string> _tracksForKeywordInsertion = new();
    private readonly Dictionary<Guid, string> _albumsForArtworkProcessing = new();

    private async Task<List<IndexEvent>> IndexDirectory(List<FileInfo> tracksInDirectory, MusicLibrary library)
    {
        var analyzedTracks = await ReadTracksInDirectory(tracksInDirectory);
        bool folderIsAlbum = analyzedTracks
            .Select(x => x.Album)
            .Distinct().Count() == 1;

        var parent = Directory.GetParent(tracksInDirectory.First().FullName)?.FullName;

        if (folderIsAlbum)
        {
            _logger.LogDebug("Indexing {Path} as album", parent);
            return await IndexAlbumBulk(analyzedTracks, library);
        }

        _logger.LogDebug("Indexing {Path} as single files", parent);
        return await IndexSingleFilesBulk(analyzedTracks, library);
    }

    private async Task<List<IndexEvent>> IndexAlbumBulk(List<ATL.Track> tracks, MusicLibrary library)
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
        List<IndexEvent> events = [];
        foreach (var trackToIndex in tracks)
        {
            var trackGenre = !string.IsNullOrEmpty(trackToIndex.Genre)
                ? await GetGenreBulk(trackToIndex.Genre)
                : null;

            events.Add(await IndexTrackBulk(artistsForTracks[trackToIndex], album, trackGenre, trackToIndex, library));
        }

        return events;
    }

    private async Task<List<IndexEvent>> IndexSingleFilesBulk(List<ATL.Track> tracks, MusicLibrary library)
    {
        List<IndexEvent> events = [];
        foreach (var track in tracks)
        {
            var artists = await ParseArtistsBulk(track.Artist, track.Title);
            var genre = !string.IsNullOrEmpty(track.Genre)
                ? await GetGenreBulk(track.Genre)
                : null;

            // Create single-track album
            var album = await GetAlbumBulk(artists, track);

            events.Add(await IndexTrackBulk(artists, album, genre, track, library));
        }
        return events;
    }

    private async Task<IndexEvent> IndexTrackBulk(
        List<ArtistWithRole> artists,
        Album album,
        Genre? genre,
        ATL.Track atlTrack,
        MusicLibrary library)
    {
        // Check if track already exists (for rescans)
        var existingTrack = await _context.Tracks
            .Include(t => t.AudioFile)
            .FirstOrDefaultAsync(t => t.AudioFile.FilePath == atlTrack.Path);

        if (existingTrack == null)
            return new IndexEvent(IndexerOperation.Create, atlTrack.Path,
                await CreateTrackBulk(artists, album, genre, atlTrack, library));
        
        return new IndexEvent(IndexerOperation.Update, atlTrack.Path, await UpdateTrackBulk(existingTrack, artists, album, genre, atlTrack));
    }

    private async Task<Track> CreateTrackBulk(
        List<ArtistWithRole> artists,
        Album album,
        Genre? genre,
        ATL.Track atlTrack,
        MusicLibrary library)
    {
        // Defer artwork processing until after bulk save completes
        if (!_albumsForArtworkProcessing.ContainsKey(album.Id))
        {
            var albumArtwork = await GetAlbumArtwork(atlTrack);
            if (albumArtwork != null)
            {
                _albumsForArtworkProcessing[album.Id] = albumArtwork;
            }
        }

        // Get or create audio metadata (cached automatically)
        var audioMetadata = await _context.AudioMetadata.GetOrAddBulk(
            am => new {am.Codec, am.Bitrate, am.SampleRate},
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
                Title = TextSanitizer.SanitizeForUtf8(!string.IsNullOrEmpty(atlTrack.Title)
                    ? atlTrack.Title
                    : Path.GetFileName(atlTrack.Path))!,
                Comment = TextSanitizer.SanitizeForUtf8(atlTrack.Comment),
                Genre = genre,
                GenreId = genre?.Id,
                DiscNumber = atlTrack.DiscNumber,
                TrackNumber = atlTrack.TrackNumber,
                DurationInSeconds = atlTrack.Duration,
                Isrc = TextSanitizer.SanitizeForUtf8(atlTrack.ISRC),
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

        // Defer keyword insertion until after SaveBulkChangesAsync
        // Build keyword string manually since navigation properties aren't populated yet
        _tracksForKeywordInsertion[track] = BuildKeywordString(artists, album, track.Title);

        _logger.LogDebug("Created new track: {TrackPath}", atlTrack.Path);
        return track;
    }

    private async Task<Track> UpdateTrackBulk(
        Track existingTrack,
        List<ArtistWithRole> newArtists,
        Album newAlbum,
        Genre? newGenre,
        ATL.Track atlTrack)
    {
        // Query existing artist IDs directly from the database without loading the collection
        // This avoids tracking the ArtistWithRole entities, which would cause conflicts later
        // when we try to add new artists with the same IDs
        var existingArtistIds = await _context.ArtistsWithRoles
            .Where(awr => awr.Tracks.Any(t => t.Id == existingTrack.Id))
            .OrderBy(awr => awr.ArtistId)
            .ThenBy(awr => awr.Role)
            .Select(awr => new { awr.ArtistId, awr.Role })
            .AsNoTracking()
            .ToListAsync();

        var newArtistIds = newArtists
            .OrderBy(a => a.ArtistId)
            .ThenBy(a => a.Role)
            .Select(a => new { a.ArtistId, a.Role })
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
        existingTrack.Title = TextSanitizer.SanitizeForUtf8(!string.IsNullOrEmpty(atlTrack.Title)
            ? atlTrack.Title
            : Path.GetFileName(atlTrack.Path))!;
        existingTrack.Comment = TextSanitizer.SanitizeForUtf8(atlTrack.Comment);
        existingTrack.DiscNumber = atlTrack.DiscNumber;
        existingTrack.TrackNumber = atlTrack.TrackNumber;
        existingTrack.DurationInSeconds = atlTrack.Duration;
        existingTrack.Isrc = TextSanitizer.SanitizeForUtf8(atlTrack.ISRC);

        // Update album - instead of creating a new album when metadata changes,
        // update the existing album's properties to preserve artwork and other data
        if (existingTrack.AlbumId != newAlbum.Id)
        {
            // Load the existing album if not already loaded
            if (existingTrack.Album == null)
            {
                await _context.Entry(existingTrack).Reference(t => t.Album).LoadAsync();
            }

            // Check if metadata changed vs completely different album
            // Update the existing album's properties instead of creating a new one
            // This preserves artwork and treats it as the same album with updated metadata
            if (existingTrack.Album != null)
            {
                var albumName = !string.IsNullOrEmpty(atlTrack.Album)
                    ? atlTrack.Album
                    : Directory.GetParent(atlTrack.Path)?.Name;

                existingTrack.Album.Name = TextSanitizer.SanitizeForUtf8(albumName)!;
                existingTrack.Album.ReleaseYear = atlTrack.Year;
                existingTrack.Album.DiscTotal = atlTrack.DiscTotal;
                existingTrack.Album.TrackTotal = atlTrack.TrackTotal;
                existingTrack.Album.CatalogNumber = TextSanitizer.SanitizeForUtf8(atlTrack.CatalogNumber);
                existingTrack.Album.UpdatedAt = DateTime.UtcNow;

                // Update label if needed
                var newLabel = await GetRecordLabelBulk(atlTrack);
                if (existingTrack.Album.LabelId != newLabel?.Id)
                {
                    existingTrack.Album.Label = newLabel;
                    existingTrack.Album.LabelId = newLabel?.Id;
                }

                _logger.LogDebug("Track {TrackPath} album metadata updated: {AlbumName}",
                    atlTrack.Path, existingTrack.Album.Name);
            }
            else
            {
                // No existing album, use the new one from GetAlbumBulk
                existingTrack.Album = newAlbum;
                existingTrack.AlbumId = newAlbum.Id;
                _logger.LogDebug("Track {TrackPath} assigned to new album: {NewAlbum}",
                    atlTrack.Path, newAlbum.Name);
            }
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
            // Load the Artists collection so we can clear it
            // We must load it here because we didn't load it earlier (to avoid tracking conflicts)
            await _context.Entry(existingTrack).Collection(t => t.Artists).LoadAsync();

            // Clear existing artist relationships (EF Core will handle the junction table)
            existingTrack.Artists.Clear();

            // Query for ArtistWithRole entities fresh from the database by their IDs
            // We fetch them fresh to get clean instances that aren't being tracked yet
            var artistIds = newArtists.Select(a => a.Id).ToList();
            var freshArtists = await _context.ArtistsWithRoles
                .Where(awr => artistIds.Contains(awr.Id))
                .ToListAsync();

            // Add the freshly-queried artist relationships
            foreach (var artistWithRole in freshArtists)
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

        // Insert keywords immediately for updated track (non-bulk, single track)
        // Reload track with all navigation properties needed by InsertKeywordsForTrack
        var reloadedTrack = await _context.Tracks
            .Include(t => t.Artists)
            .ThenInclude(a => a.Artist)
            .Include(t => t.Album)
            .ThenInclude(a => a.Label)
            .Include(t => t.Keywords)
            .FirstAsync(t => t.Id == existingTrack.Id);

        await _searchService.InsertKeywordsForTrack(reloadedTrack);

        _logger.LogDebug("Updated existing track: {TrackPath}", atlTrack.Path);
        return reloadedTrack;
    }


    private async Task<Genre> GetGenreBulk(string genreName)
    {
        var sanitizedName = TextSanitizer.SanitizeForUtf8(genreName)!;

        return await _context.Genres.GetOrAddBulk(
            keySelector: g => g.Name,
            createFunc: () => new Genre
            {
                Id = Guid.NewGuid(),
                Name = sanitizedName,
                CreatedAt = DateTime.UtcNow
            });
    }

    private async Task<Artist> GetArtistBulk(string artistName)
    {
        if (string.IsNullOrEmpty(artistName)) artistName = "Unknown Artist";

        // Sanitize artist name for UTF-8 compatibility
        var sanitizedName = TextSanitizer.SanitizeForUtf8(artistName)!;

        return await _context.Artists.GetOrAddBulk(
            keySelector: a => a.Name,
            createFunc: () =>
            {
                _logger.LogDebug("Creating new artist: {Artist}", sanitizedName);
                return new Artist
                {
                    Id = Guid.NewGuid(),
                    Name = sanitizedName,
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
                keySelector: awr => new {awr.ArtistId, awr.Role},
                createFunc: () => new ArtistWithRole
                {
                    Id = Guid.NewGuid(),
                    Artist = artist,
                    ArtistId = artist.Id,
                    Role = role,
                    CreatedAt = DateTime.UtcNow
                });

            // Ensure Artist navigation property is always set (important for keyword string generation)
            // GetOrAddBulk might return an existing entity from DB without navigation properties loaded
            if (artistWithRole.Artist == null)
            {
                artistWithRole.Artist ??= artist;
            }

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

        var sanitizedName = TextSanitizer.SanitizeForUtf8(labelName)!;

        return await _context.RecordLabels.GetOrAddBulk(
            keySelector: l => l.Name,
            createFunc: () => new RecordLabel
            {
                Id = Guid.NewGuid(),
                Name = sanitizedName,
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
                Name = TextSanitizer.SanitizeForUtf8(albumName)!,
                ReleaseYear = atlTrack.Year,
                DiscTotal = atlTrack.DiscTotal,
                TrackTotal = atlTrack.TrackTotal,
                CatalogNumber = TextSanitizer.SanitizeForUtf8(atlTrack.CatalogNumber),
                Label = label,
                CreatedAt = DateTime.UtcNow
            });

        // Register album-artist relationships if not already present
        foreach (var artistWithRole in artists)
        {
            _context.AddRelationshipBulk(album, artistWithRole);
        }

        return album;
    }
    
    private List<string> SplitArtist(string? artistName)
    {
        if (artistName == null) return new List<string>();
        string[] splitChars = [",", "&", ";", " x "];
        var split = artistName.Split(splitChars, StringSplitOptions.TrimEntries);
        return split.Distinct().ToList();
    }

    private static string? ParseRemixers(string title)
    {
        // supports both (artist remix) and [artist remix]
        var remixerMatch = RemixerParsingRegex.Match(title).Groups.Values.Where(a => !string.IsNullOrEmpty(a.Value));
        var parsedRemixers = remixerMatch.LastOrDefault()?.Value.Trim();
        return parsedRemixers;
    }

    private static string? ParseFeaturingArtists(string title)
    {
        var featuringMatch = FeaturingArtistParsingRegex.Match(title);
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
    
    private static string BuildKeywordString(IEnumerable<ArtistWithRole> artists, Album album, string trackTitle)
    {
        var artistString = string.Join(", ", artists.Select(a => a.Artist.Name));
        var releaseYear = album.ReleaseYear != null ? $"({album.ReleaseYear})" : "";
        var label = album.Label != null ? $"({album.Label.Name} - {album.CatalogNumber})" : "";
        return $"{artistString} - {trackTitle} - {album.Name} {releaseYear} {label}";
    }

    private async Task InsertKeywordsForTracksBulkAsync(Dictionary<Track, string> trackKeywordStrings)
    {
        if (!trackKeywordStrings.Any()) return;

        // Note: Keyword strings are pre-computed during CreateTrackBulk
        // when navigation properties are still available. This avoids reloading 50k+ tracks
        // with all their navigation properties just to generate keyword strings.
        // Updates use InsertKeywordsForTrack (non-bulk) directly in UpdateTrackBulk.

        // Insert keywords for all tracks in bulk
        await _searchService.InsertKeywordsForTracksBulk(trackKeywordStrings);
    }

    private async Task<List<IndexEvent>> DeleteMissingTracks(MusicLibrary library)
    {
        var indexedFiles = _context.AudioFiles
            .Where(f => f.Library.Id == library.Id)
            .AsEnumerable();

        var missingFiles = indexedFiles
            .Where(f => !Path.Exists(f.FilePath))
            .Select(f => new { f.Id, f.FilePath })
            .ToList();
        var missingFileIds =  missingFiles.Select(f => f.Id).ToList();

        if (!missingFiles.Any()) return [];

        // Get track IDs before deleting them (for embedding cleanup)
        var trackIds = await _context.Tracks
            .Where(t => missingFileIds.Contains(t.AudioFile.Id))
            .Select(t => t.Id)
            .ToListAsync();

        var deletedTracks = await _context.Tracks
            .Where(f => missingFileIds.Contains(f.AudioFile.Id))
            .ExecuteDeleteAsync();

        if (deletedTracks > 0)
        {
            _logger.LogInformation("Deleted {Tracks} missing tracks", deletedTracks);

            // Delete embeddings from DuckDB
            if (trackIds.Any())
            {
                try
                {
                    await _embeddingService.DeleteEmbeddingsAsync(trackIds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete embeddings for {Count} tracks", trackIds.Count);
                }
            }
        }

        await DeleteEmptyArtistsAndAlbums();

        // Finally, delete the missing file entries
        await _context.AudioFiles
            .Where(f => missingFileIds.Contains(f.Id))
            .ExecuteDeleteAsync();
        
        return missingFiles.Select(m => new IndexEvent(IndexerOperation.Delete, m.FilePath, null)).ToList();
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
            .Include(t => t.Artwork)
            .Where(a => !a.Tracks.Any() && a.Artwork != null)
            .Select(a => a.Artwork!)
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
    
    public async IAsyncEnumerable<IndexEvent> IndexDirectoryGroups(
        IAsyncEnumerable<DirectoryGroup> directoryGroups,
        MusicLibrary library,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        library = await GetLibrary(library);

        var events = await DeleteMissingTracks(library);
        foreach (var deleteEvent in events)
        {
            yield return deleteEvent;
        }

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

            // Index the directory (stores in bulk context, doesn't save yet)
            foreach (var indexEvent in await IndexDirectory(filesInDirectory, library))
            {
                yield return indexEvent;
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

        // Process artworks in parallel (CPU-intensive image operations)
        _logger.LogInformation("Processing artworks for {AlbumCount} albums in parallel",
            _albumsForArtworkProcessing.Count);
        var artworkEntities = await _artworkService.ProcessArtworksParallel(_albumsForArtworkProcessing);
        _albumsForArtworkProcessing.Clear();

        // Sequentially add artwork entities to BulkContext (thread-safe)
        foreach (var artwork in artworkEntities)
        {
            await _context.Artworks.GetOrAddBulk(
                a => new {a.AlbumId},
                () => artwork);
        }

        // Save all bulk operations but retain cache for keyword relationship registration
        var stats = await _context.SaveBulkChangesAsync(
            new BulkInsertOptions {Logger = _logger},
            retainCache: true,
            cancellationToken);

        _logger.LogInformation(
            "Bulk saved {Entities} entities and {Relationships} relationships in {Time:F2}s ({Rate:N0} entities/sec)",
            stats.TotalEntitiesInserted,
            stats.TotalRelationshipsInserted,
            stats.TotalTime.TotalSeconds,
            stats.TotalEntitiesInserted / stats.TotalTime.TotalSeconds);

        // Clear change tracker before keyword insertion to avoid stale entity conflicts
        _context.ChangeTracker.Clear();

        // Insert keywords and relationships into BulkContext (tracks still cached from previous save)
        await InsertKeywordsForTracksBulkAsync(_tracksForKeywordInsertion);
        _tracksForKeywordInsertion.Clear();

        // Save keywords and relationships in one bulk operation, now clear cache
        var keywordStats = await _context.SaveBulkChangesAsync(
            new BulkInsertOptions {Logger = _logger},
            retainCache: false,
            cancellationToken);

        _logger.LogInformation(
            "Bulk saved {Entities} keyword entities and {Relationships} keyword relationships in {Time:F2}s",
            keywordStats.TotalEntitiesInserted,
            keywordStats.TotalRelationshipsInserted,
            keywordStats.TotalTime.TotalSeconds);

        // Clear change tracker before cleanup to ensure fresh queries
        _context.ChangeTracker.Clear();

        // Clean up orphaned albums and artists (from album/genre changes during rescans)
        await DeleteEmptyArtistsAndAlbums();

        _logger.LogInformation("Finalized indexing for {Directory}", library.LibraryPath);
        library.LastScan = DateTime.UtcNow;

        // Save library metadata
        await _context.SaveChangesAsync(cancellationToken);
    }
}