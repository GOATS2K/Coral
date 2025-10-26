using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Events;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Coral.Services.ChannelWrappers;

namespace Coral.Services;

public interface IIndexerService
{
    public Task DeleteTrack(string filePath);
    public Task HandleRename(string oldPath, string newPath);
    public Task ScanDirectory(string directory, MusicLibrary library);
    public Task ScanLibraries();
    public Task ScanLibrary(MusicLibrary library, bool incremental = false);
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<IndexerService> _logger;

    private static readonly string[] AudioFileFormats =
        [".flac", ".mp3", ".mp2", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus"];

    private static readonly string[] ImageFileFormats = [".jpg", ".png"];
    private readonly MusicLibraryRegisteredEventEmitter _musicLibraryRegisteredEventEmitter;
    private readonly IMapper _mapper;
    private readonly IEmbeddingChannel _embeddingChannel;

    public IndexerService(CoralDbContext context, ISearchService searchService, ILogger<IndexerService> logger,
        IArtworkService artworkService, MusicLibraryRegisteredEventEmitter eventEmitter, IMapper mapper, IEmbeddingChannel embeddingChannel)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
        _musicLibraryRegisteredEventEmitter = eventEmitter;
        _mapper = mapper;
        _embeddingChannel = embeddingChannel;
    }

    public async Task<List<MusicLibraryDto>> GetMusicLibraries()
    {
        return await
            _context
                .MusicLibraries
                .ProjectTo<MusicLibraryDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
    }

    public async Task<MusicLibrary?> AddMusicLibrary(string path)
    {
        try
        {
            var contentDirectory = new DirectoryInfo(path);
            if (!contentDirectory.Exists)
            {
                throw new ApplicationException("Content directory does not exist.");
            }

            var library = await _context.MusicLibraries.FirstOrDefaultAsync(m => m.LibraryPath == path)
                          ?? new MusicLibrary()
                          {
                              LibraryPath = path,
                              AudioFiles = new List<AudioFile>()
                          };
            _context.MusicLibraries.Add(library);
            await _context.SaveChangesAsync();
            _musicLibraryRegisteredEventEmitter.EmitEvent(library);
            return library;
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to add music library {Path} due to exception: {Exception}", path, e.ToString());
            return null;
        }
    }

    public async Task ScanLibraries()
    {
        foreach (var musicLibrary in _context.MusicLibraries.ToList())
        {
            // existing test libraries are ""
            // new libraries are inserted via AddMusicLibrary
            // which verifies that the library actually exists
            if (musicLibrary.LibraryPath == "") continue;
            await ScanLibrary(musicLibrary);
        }
    }

    public async Task ScanLibrary(MusicLibrary library, bool incremental = false)
    {
        // fix unique constraint issues by getting a fresh copy of MusicLibrary for each indexing job
        library = await GetLibrary(library);
        // first delete missing tracks
        await DeleteMissingTracks(library);
        // then scan library
        var directoryGroups = await ScanMusicLibraryAsync(library, incremental);

        // enumerate directories
        var foldersScanned = 0;
        foreach (var directoryGroup in directoryGroups)
        {
            var tracksInDirectory = directoryGroup.ToList();
            if (!tracksInDirectory.Any())
            {
                _logger.LogWarning("Skipping empty directory {Directory}", directoryGroup.Key);
                continue;
            }

            await IndexDirectory(tracksInDirectory, library);

            foldersScanned++;
            _logger.LogInformation("Completed indexing of {Path}", directoryGroup.Key);
            // the change tracker consumes a lot of memory and 
            // progressively slows down the indexing process
            if (foldersScanned % 25 == 0)
            {
                _context.ChangeTracker.Clear();
                library = await GetLibrary(library);
            }
        }

        _logger.LogInformation("Completed scan of {Directory}", library.LibraryPath);
        library.LastScan = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task ScanDirectory(string directory, MusicLibrary library)
    {
        library = await GetLibrary(library);
        if (directory == library.LibraryPath)
        {
            await ScanLibrary(library, incremental: true);
            return;
        }

        var contentDirectory = new DirectoryInfo(directory);
        if (!contentDirectory.Exists)
        {
            _logger.LogError("Directory {Directory} does not exist", directory);
            return;
        }

        await DeleteMissingTracks(library);
        var tracksInDirectory = contentDirectory.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)))
            .ToList();
        await IndexDirectory(tracksInDirectory, library);

        library.LastScan = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task HandleRename(string oldPath, string newPath)
    {
        if (File.Exists(newPath))
        {
            await HandleFileRename(oldPath, newPath);
        }

        if (Directory.Exists(newPath))
        {
            await HandleDirectoryRename(oldPath, newPath);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteTrack(string filePath)
    {
        // delete missing tracks
        var indexedFile = await _context.AudioFiles.FirstOrDefaultAsync(f => f.FilePath == filePath);
        if (indexedFile == null)
        {
            throw new ArgumentException($"File {filePath} has not previously been indexed.");
        }

        var deletedTrack = await _context.Tracks.Where(f => f.AudioFile.Id == indexedFile.Id).ExecuteDeleteAsync();
        if (deletedTrack > 0) _logger.LogInformation("Deleted track: {FilePath}", filePath);

        // then, delete all tracks and artists with 0 tracks
        await DeleteEmptyArtistsAndAlbums();

        // finally, delete the missing file entries
        _context.Remove(indexedFile);
        await _context.SaveChangesAsync();
    }

    private async Task HandleDirectoryRename(string oldPath, string newPath)
    {
        var filesToMove = _context.AudioFiles.Where(t => t.FilePath.StartsWith(oldPath)).ToList();
        foreach (var file in filesToMove)
        {
            file.FilePath = file.FilePath.Replace(oldPath, newPath);
            _context.Update(file);
        }

        var artwork = await _context.Artworks.FirstOrDefaultAsync(a => a.Path.StartsWith(oldPath));
        if (artwork != null)
        {
            artwork.Path = artwork.Path.Replace(oldPath, newPath);
            _context.Update(artwork);
        }
    }

    private async Task HandleFileRename(string oldPath, string newPath)
    {
        var targetFile = await _context.AudioFiles.FirstOrDefaultAsync(t => t.FilePath == oldPath);
        if (targetFile == null)
        {
            throw new ArgumentException($"File {oldPath} has not previously been indexed");
        }
        targetFile.FilePath = newPath;
    }

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
            await IndexAlbum(analyzedTracks, library);
        }
        else
        {
            _logger.LogDebug("Indexing {Path} as single files", parent);
            await IndexSingleFiles(analyzedTracks, library);
        }
    }

    private async Task<IEnumerable<IGrouping<string?, FileInfo>>> ScanMusicLibraryAsync(MusicLibrary library, bool incremental = false)
    {
        var contentDirectory = new DirectoryInfo(library.LibraryPath);
        Func<FileInfo, bool> predicate;

        if (!incremental)
        {
            _logger.LogInformation("Starting full scan of directory: {Directory}", library.LibraryPath);
            var existingFiles = await _context.AudioFiles.Where(f => f.Library.Id == library.Id).ToListAsync();
            predicate = f =>
                AudioFileFormats.Contains(Path.GetExtension(f.FullName))
                && !existingFiles.Any(t => t.FilePath == f.FullName && f.LastWriteTimeUtc == t.UpdatedAt);
        }
        else
        {
            _logger.LogInformation("Starting incremental scan of directory: {Directory}", library.LibraryPath);
            predicate = f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) 
            && (f.LastWriteTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan);
        }

        var directoryGroups = contentDirectory.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(predicate)
            .GroupBy(f => f.Directory?.Name, f => f);
        return directoryGroups;
    }

    private async Task DeleteMissingTracks(MusicLibrary library)
    {
        // delete missing tracks
        var indexedFiles = _context.AudioFiles.Where(f => f.Library.Id == library.Id).AsEnumerable();
        var missingFiles = indexedFiles.Where(f => !Path.Exists(f.FilePath)).Select(f => f.Id);

        var deletedTracks = await _context.Tracks.Where(f => missingFiles.Contains(f.AudioFile.Id)).ExecuteDeleteAsync();
        if (deletedTracks > 0) _logger.LogInformation("Deleted {Tracks} missing tracks", deletedTracks);
        await DeleteEmptyArtistsAndAlbums();

        // finally, delete the missing file entries
        await _context.AudioFiles.Where(f => missingFiles.Contains(f.Id)).ExecuteDeleteAsync();
    }

    private async Task DeleteEmptyArtistsAndAlbums()
    {
        // then, delete all tracks and artists with 0 tracks
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

    private async Task<MusicLibrary> GetLibrary(MusicLibrary library)
    {
        var existingLibrary = await _context.MusicLibraries.FindAsync(library.Id);
        if (existingLibrary != null)
        {
            return existingLibrary;
        }

        return library;
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

    private async Task IndexSingleFiles(List<ATL.Track> tracks, MusicLibrary library)
    {
        foreach (var atlTrack in tracks)
        {
            var artists = await ParseArtists(atlTrack.Artist, atlTrack.Title);
            var indexedAlbum = await GetAlbum(artists, atlTrack);
            var indexedGenre = await GetGenre(atlTrack.Genre);
            await IndexFile(artists, indexedAlbum, indexedGenre, atlTrack, library);
        }
    }

    private async Task IndexAlbum(List<ATL.Track> tracks, MusicLibrary library)
    {
        var distinctGenres = tracks.Where(t => t.Genre != null)
            .Select(t => t.Genre)
            .Distinct();
        var createdGenres = new List<Genre>();

        // parse all artists
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

        // most attributes are going to be the same in an album
        var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();

        var albumType =
            AlbumTypeHelper.GetAlbumType(distinctArtists.Count(a => a.Role == ArtistRole.Main), tracks.Count());
        var indexedAlbum = await GetAlbum(distinctArtists, tracks.First());
        indexedAlbum.Type = albumType;
        foreach (var trackToIndex in tracks)
        {
            var targetGenre = createdGenres.FirstOrDefault(g => g.Name == trackToIndex.Genre);
            await IndexFile(artistForTracks[trackToIndex], indexedAlbum, targetGenre, trackToIndex, library);
        }
    }

    private async Task IndexFile(List<ArtistWithRole> artists, Album indexedAlbum, Genre? indexedGenre,
        ATL.Track atlTrack, MusicLibrary library)
    {
        // this can happen if the scan process is interrupted
        // and then resumed again
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

        await _embeddingChannel.GetWriter().WriteAsync(new EmbeddingJob(indexedTrack, null));
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
        // supports both (artist remix) and [artist remix]
        var pattern = @"\(([^()]*)(?: Edit| Remix| VIP| Bootleg)\)|\[([^[\[\]]*)(?: Edit| Remix| VIP| Bootleg)\]";
        var expression = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var remixerMatch = expression.Match(title).Groups.Values.Where(a => !string.IsNullOrEmpty(a.Value));
        // first group is parenthesis, second is brackets
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
        // get artwork from file parent folder
        var albumDirectory = new DirectoryInfo(atlTrack.Path)
            .Parent;

        var artwork = albumDirectory?.EnumerateFiles("*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => ImageFileFormats.Contains(Path.GetExtension(f.FullName)));

        return artwork?.FullName ?? await _artworkService.ExtractEmbeddedArtwork(atlTrack);
    }

    private async Task<Album> GetAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack)
    {
        var albumName = !string.IsNullOrEmpty(atlTrack.Album)
            ? atlTrack.Album
            : Directory.GetParent(atlTrack.Path)?.Name;
        // Albums can have the same name, so in order to differentiate between them
        // we also use supplemental metadata. 
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
}