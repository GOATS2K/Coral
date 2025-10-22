using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coral.Services.Indexer;

public interface INewIndexerService
{
    IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<DirectoryGroup> directoryGroups,
        MusicLibrary library,
        CancellationToken cancellationToken = default);
    Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default);
}

public class NewIndexerService : INewIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<NewIndexerService> _logger;

    private static readonly string[] ImageFileFormats = [".jpg", ".png"];
    private int _foldersScanned = 0;

    public NewIndexerService(
        CoralDbContext context,
        ISearchService searchService,
        IArtworkService artworkService,
        ILogger<NewIndexerService> logger)
    {
        _context = context;
        _searchService = searchService;
        _artworkService = artworkService;
        _logger = logger;
    }

    public async IAsyncEnumerable<Track> IndexDirectoryGroups(
        IAsyncEnumerable<DirectoryGroup> directoryGroups,
        MusicLibrary library,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var directoryGroup in directoryGroups.WithCancellation(cancellationToken))
        {
            if (directoryGroup.Files.Count == 0)
            {
                _logger.LogWarning("Skipping empty directory {Directory}", directoryGroup.DirectoryPath);
                continue;
            }

            var tracks = await IndexDirectory(directoryGroup, library);

            foreach (var track in tracks)
            {
                yield return track;
            }

            _foldersScanned++;
            _logger.LogInformation("Completed indexing of {Path}", directoryGroup.DirectoryPath);

            if (_foldersScanned % 25 == 0)
            {
                _context.ChangeTracker.Clear();
                library = await GetLibrary(library);
            }
        }
    }

    private async Task<List<Track>> IndexDirectory(DirectoryGroup directoryGroup, MusicLibrary library)
    {
        var analyzedTracks = await ReadTracksInDirectory(directoryGroup.Files);
        bool folderIsAlbum = analyzedTracks
            .Select(x => x.Album)
            .Distinct().Count() == 1;

        if (folderIsAlbum)
        {
            _logger.LogDebug("Indexing {Path} as album", directoryGroup.DirectoryPath);
            return await IndexAlbum(analyzedTracks, library);
        }
        else
        {
            _logger.LogDebug("Indexing {Path} as single files", directoryGroup.DirectoryPath);
            return await IndexSingleFiles(analyzedTracks, library);
        }
    }

    private async Task<List<ATL.Track>> ReadTracksInDirectory(List<FileInfo> tracksInDirectory)
    {
        var maxRetries = 10;
        var analyzedTracks = new List<ATL.Track>();
        foreach (var file in tracksInDirectory)
        {
            var retries = 0;
            ATL.Track? analyzedTrack = null;
            while (analyzedTrack == null && retries != maxRetries)
            {
                try
                {
                    analyzedTrack = new ATL.Track(file.FullName);
                }
                catch (SystemException ex)
                {
                    _logger.LogError("Failed to read {File} due to Exception: {Exception} - retrying...",
                        file.FullName, ex.ToString());
                    retries++;
                    await Task.Delay(1000);
                }
            }

            if (retries == maxRetries)
            {
                _logger.LogError("Failed to read track {Track} due to I/O errors", file.FullName);
                continue;
            }

            if (analyzedTrack?.DurationMs > 0)
            {
                analyzedTracks.Add(analyzedTrack);
            }
        }

        return analyzedTracks;
    }

    private async Task<List<Track>> IndexSingleFiles(List<ATL.Track> tracks, MusicLibrary library)
    {
        var indexedTracks = new List<Track>();
        foreach (var atlTrack in tracks)
        {
            var artists = await ParseArtists(atlTrack.Artist, atlTrack.Title);
            var indexedAlbum = await GetAlbum(artists, atlTrack);
            var indexedGenre = await GetGenre(atlTrack.Genre);
            var track = await IndexFile(artists, indexedAlbum, indexedGenre, atlTrack, library);
            indexedTracks.Add(track);
        }
        return indexedTracks;
    }

    private async Task<List<Track>> IndexAlbum(List<ATL.Track> tracks, MusicLibrary library)
    {
        var indexedTracks = new List<Track>();
        var distinctGenres = tracks.Where(t => t.Genre != null)
            .Select(t => t.Genre)
            .Distinct();
        var createdGenres = new List<Genre>();

        var artistForTracks = new Dictionary<ATL.Track, List<ArtistWithRole>>();
        foreach (var track in tracks)
        {
            var artists = await ParseArtists(track.Artist, track.Title);
            artistForTracks.Add(track, artists);
        }

        foreach (var genre in distinctGenres)
        {
            var indexedGenre = await GetGenre(genre);
            createdGenres.Add(indexedGenre);
        }

        var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();

        var albumType =
            AlbumTypeHelper.GetAlbumType(distinctArtists.Count(a => a.Role == ArtistRole.Main), tracks.Count());
        var indexedAlbum = await GetAlbum(distinctArtists, tracks.First());
        indexedAlbum.Type = albumType;
        foreach (var trackToIndex in tracks)
        {
            var targetGenre = createdGenres.FirstOrDefault(g => g.Name == trackToIndex.Genre);
            var track = await IndexFile(artistForTracks[trackToIndex], indexedAlbum, targetGenre, trackToIndex, library);
            indexedTracks.Add(track);
        }
        return indexedTracks;
    }

    private async Task<Track> IndexFile(List<ArtistWithRole> artists, Album indexedAlbum, Genre? indexedGenre,
        ATL.Track atlTrack, MusicLibrary library)
    {
        if (indexedAlbum.Artworks == null)
        {
            indexedAlbum.Artworks = new List<Artwork>();
        }

        if (!indexedAlbum.Artworks.Any())
        {
            try
            {
                var albumArtwork = await GetAlbumArtwork(atlTrack);
                if (albumArtwork != null) await _artworkService.ProcessArtwork(indexedAlbum, albumArtwork);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get artwork for track: {Track} due to the following exception: {Exception}",
                    atlTrack.Path, ex.ToString());
            }
        }

        var indexedTrack = await AddOrUpdateTrack(artists, indexedAlbum, indexedGenre, atlTrack, library);
        await _searchService.InsertKeywordsForTrack(indexedTrack);
        return indexedTrack;
    }

    private async Task<Track> AddOrUpdateTrack(List<ArtistWithRole> artists, Album indexedAlbum, Genre? indexedGenre, ATL.Track atlTrack, MusicLibrary library)
    {
        var indexedTrack = await _context.Tracks
            .Include(t => t.AudioFile)
            .Include(t => t.Keywords)
            .Include(t => t.Artists)
            .Include(t => t.Album)
            .FirstOrDefaultAsync(t => t.AudioFile.FilePath == atlTrack.Path);

        if (indexedTrack == null) indexedTrack = new();
        indexedTrack.Album = indexedAlbum;
        indexedTrack.Artists = artists;
        indexedTrack.Title = !string.IsNullOrEmpty(atlTrack.Title) ? atlTrack.Title : Path.GetFileName(atlTrack.Path);
        indexedTrack.Comment = atlTrack.Comment;
        indexedTrack.Genre = indexedGenre;
        indexedTrack.DiscNumber = atlTrack.DiscNumber;
        indexedTrack.TrackNumber = atlTrack.TrackNumber;
        indexedTrack.DurationInSeconds = atlTrack.Duration;
        indexedTrack.Isrc = atlTrack.ISRC;
        indexedTrack.Keywords ??= new List<Keyword>();
        indexedTrack.AudioFile = new AudioFile()
        {
            FilePath = atlTrack.Path,
            UpdatedAt = File.GetLastWriteTimeUtc(atlTrack.Path),
            FileSizeInBytes = new FileInfo(atlTrack.Path).Length,
            AudioMetadata = GetAudioMetadata(atlTrack),
            Library = library
        };
        _logger.LogDebug("Indexing track: {TrackPath}", atlTrack.Path);

        if (indexedTrack.Id == Guid.Empty)
        {
            indexedTrack.Id = Guid.NewGuid();
            _context.Tracks.Add(indexedTrack);
        }
        else
        {
            indexedTrack.UpdatedAt = DateTime.UtcNow;
            _context.Tracks.Update(indexedTrack);
        }

        return indexedTrack;
    }

    private AudioMetadata GetAudioMetadata(ATL.Track atlTrack)
    {
        var metadata = new AudioMetadata()
        {
            Bitrate = atlTrack.Bitrate,
            Channels = atlTrack.ChannelsArrangement?.NbChannels,
            SampleRate = atlTrack.SampleRate,
            Codec = atlTrack.AudioFormat.ShortName,
            BitDepth = atlTrack.BitDepth != -1 ? atlTrack.BitDepth : null,
        };
        return metadata;
    }

    private async Task<Genre> GetGenre(string genreName)
    {
        var indexedGenre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == genreName);
        if (indexedGenre == null)
        {
            indexedGenre = new Genre()
            {
                Name = genreName,
            };
            _context.Genres.Add(indexedGenre);
        }

        return indexedGenre;
    }

    private async Task<Artist> GetArtist(string artistName)
    {
        if (string.IsNullOrEmpty(artistName)) artistName = "Unknown Artist";
        var indexedArtist = _context.Artists.FirstOrDefault(a => a.Name == artistName);
        if (indexedArtist == null)
        {
            indexedArtist = new Artist()
            {
                Name = artistName,
            };
            _context.Artists.Add(indexedArtist);
            _logger.LogDebug("Creating new artist: {Artist}", artistName);
            await _context.SaveChangesAsync();
        }

        return indexedArtist;
    }

    private List<string> SplitArtist(string? artistName)
    {
        if (artistName == null) return new List<string>();
        string[] splitChars = { ",", "&", ";", " x " };
        var split = artistName.Split(splitChars, StringSplitOptions.TrimEntries);
        return split.Distinct().ToList();
    }

    private async Task<List<ArtistWithRole>> GetArtistWithRole(List<string> artists, ArtistRole role)
    {
        var artistsWithRoles = new List<ArtistWithRole>();
        foreach (var artist in artists)
        {
            var indexedArtist = await GetArtist(artist.Trim());
            var artistWithRole =
                _context.ArtistsWithRoles.FirstOrDefault(a => a.ArtistId == indexedArtist.Id && a.Role == role) ??
                new ArtistWithRole()
                {
                    Artist = indexedArtist,
                    Role = role
                };
            artistsWithRoles.Add(artistWithRole);
        }

        return artistsWithRoles;
    }

    private async Task<List<ArtistWithRole>> ParseArtists(string artist, string title)
    {
        var parsedFeaturingArtists = ParseFeaturingArtists(title);
        var parsedRemixers = ParseRemixers(title);

        var guestArtists = !string.IsNullOrEmpty(parsedFeaturingArtists)
            ? await GetArtistWithRole(SplitArtist(parsedFeaturingArtists), ArtistRole.Guest)
            : new List<ArtistWithRole>();
        var remixers = !string.IsNullOrEmpty(parsedRemixers)
            ? await GetArtistWithRole(SplitArtist(parsedRemixers), ArtistRole.Remixer)
            : new List<ArtistWithRole>();
        var mainArtists = await GetArtistWithRole(SplitArtist(artist), ArtistRole.Main);

        var artistList = new List<ArtistWithRole>();
        artistList.AddRange(guestArtists);
        artistList.AddRange(mainArtists);
        artistList.AddRange(remixers);

        return artistList;
    }

    private static string? ParseRemixers(string title)
    {
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

    private async Task<Album> GetAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack)
    {
        var albumName = !string.IsNullOrEmpty(atlTrack.Album)
            ? atlTrack.Album
            : Directory.GetParent(atlTrack.Path)?.Name;

        var albumQuery = _context.Albums
            .Include(a => a.Artists)
            .Include(a => a.Tracks)
            .Where(a => a.Name == albumName && a.ReleaseYear == atlTrack.Year && a.DiscTotal == atlTrack.DiscTotal &&
                        a.TrackTotal == atlTrack.TrackTotal);
        var indexedAlbum = await albumQuery.FirstOrDefaultAsync() ?? await CreateAlbum(artists, atlTrack, albumName);

        if (!indexedAlbum.Artists
                .Select(a => a.ArtistId)
                .Order()
                .SequenceEqual(artists.Select(a => a.ArtistId).Order()))
        {
            var missingArtists = artists.Where(a => !indexedAlbum.Artists.Contains(a));
            indexedAlbum.Artists.AddRange(missingArtists);
            _context.Albums.Update(indexedAlbum);
        }

        return indexedAlbum;
    }

    private async Task<RecordLabel?> GetRecordLabel(ATL.Track atlTrack)
    {
        var trackHasLabel = atlTrack.AdditionalFields.TryGetValue("LABEL", out var labelName);
        if (!trackHasLabel) return null;
        var existingLabel = await _context.RecordLabels.FirstOrDefaultAsync(t => t.Name == labelName);
        if (existingLabel is not null) return existingLabel;

        var label = new RecordLabel()
        {
            Name = labelName!,
            CreatedAt = DateTime.UtcNow,
        };
        _context.RecordLabels.Add(label);
        return label;
    }

    private async Task<Album> CreateAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack, string? albumName)
    {
        var label = await GetRecordLabel(atlTrack);
        var album = new Album()
        {
            Artists = new List<ArtistWithRole>(),
            Name = albumName!,
            ReleaseYear = atlTrack.Year,
            DiscTotal = atlTrack.DiscTotal,
            TrackTotal = atlTrack.TrackTotal,
            CatalogNumber = atlTrack.CatalogNumber,
            CreatedAt = DateTime.UtcNow,
            Artworks = new List<Artwork>()
        };
        if (label is not null)
            album.Label = label;

        _context.Albums.Add(album);
        album.Artists.AddRange(artists);
        return album;
    }

    private async Task<MusicLibrary> GetLibrary(MusicLibrary library)
    {
        var existingLibrary = await _context.MusicLibraries.FindAsync(library.Id);
        if (existingLibrary != null)
        {
            return existingLibrary;
        }

        return library;
    }

    public async Task FinalizeIndexing(MusicLibrary library, CancellationToken cancellationToken = default)
    {
        await DeleteMissingTracks(library);

        library.LastScan = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await DeleteEmptyArtistsAndAlbums();
    }

    private async Task DeleteMissingTracks(MusicLibrary library)
    {
        var indexedFiles = _context.AudioFiles.Where(f => f.Library.Id == library.Id).AsEnumerable();
        var missingFiles = indexedFiles.Where(f => !Path.Exists(f.FilePath)).Select(f => f.Id);

        var deletedTracks = await _context.Tracks.Where(f => missingFiles.Contains(f.AudioFile.Id)).ExecuteDeleteAsync();
        if (deletedTracks > 0) _logger.LogInformation("Deleted {Tracks} missing tracks", deletedTracks);

        await _context.AudioFiles.Where(f => missingFiles.Contains(f.Id)).ExecuteDeleteAsync();
    }

    private async Task DeleteEmptyArtistsAndAlbums()
    {
        var deletedArtists = await _context.ArtistsWithRoles.Where(a => !a.Tracks.Any()).ExecuteDeleteAsync();
        if (deletedArtists > 0) _logger.LogInformation("Deleted {DeletedArtists} artists with no tracks", deletedArtists);

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

        var deletedAlbums = await _context.Albums.Where(a => !a.Tracks.Any()).ExecuteDeleteAsync();
        if (deletedAlbums > 0) _logger.LogInformation("Deleted {DeletedAlbums} albums with no tracks", deletedAlbums);
    }
}
