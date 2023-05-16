using Coral.Database;
using Coral.Database.Models;
using Coral.Events;
using Coral.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;

namespace Coral.Services;

public interface IIndexerService
{
    public Task ReadLibraries();
    public Task<MusicLibrary?> AddMusicLibrary(string path);
    public Task ReadDirectory(MusicLibrary library);
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<IndexerService> _logger;
    private static readonly string[] AudioFileFormats = { ".flac", ".mp3", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus" };
    private static readonly string[] ImageFileFormats = { ".jpg", ".png" };
    private readonly MusicLibraryRegisteredEventEmitter _musicLibraryRegisteredEventEmitter;

    public IndexerService(CoralDbContext context, ISearchService searchService, ILogger<IndexerService> logger, IArtworkService artworkService, MusicLibraryRegisteredEventEmitter eventEmitter)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
        _musicLibraryRegisteredEventEmitter = eventEmitter;
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

            var library = await _context.MusicLibraries.FirstOrDefaultAsync(m => m.LibraryPath == path);
            if (library == null)
            {
                library = new MusicLibrary()
                {
                    LibraryPath = path,
                };
            }
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


    private bool ContentDirectoryNeedsRescan(MusicLibrary library)
    {
        if (library.LastScan == DateTime.MinValue)
        {
            return true;
        }

        var contentDirectory = new DirectoryInfo(library.LibraryPath);
        return contentDirectory.LastAccessTimeUtc > library.LastScan;
    }

    public async Task ReadLibraries()
    {
        foreach (var musicLibrary in _context.MusicLibraries.ToList())
        {
            await ReadDirectory(musicLibrary);
        }
    }

    public async Task ReadDirectory(MusicLibrary library)
    {
        if (!ContentDirectoryNeedsRescan(library)) return;

        // fix unique constraint issues by getting a fresh copy of MusicLibrary for each indexing job
        if (library.Id != 0)
        {
            var existingLibrary = await _context.MusicLibraries.FindAsync(library.Id);
            if (existingLibrary != null)
            {
                library = existingLibrary;
            }
        }

        _logger.LogInformation("Scanning directory: {Directory}", library.LibraryPath);
        var contentDirectory = new DirectoryInfo(library.LibraryPath);
        var directoryGroups = contentDirectory.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) && (f.LastAccessTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan))
            .GroupBy(f => f.Directory?.Name, f => f);

        // enumerate directories
        foreach (var directoryGroup in directoryGroups)
        {
            var tracksInDirectory = directoryGroup.ToList();
            if (!tracksInDirectory.Any())
            {
                _logger.LogWarning("Skipping empty directory {directory}", directoryGroup.Key);
                continue;
            }

            var analyzedTracks = await ReadTracksInDirectory(tracksInDirectory);
            bool folderIsAlbum = analyzedTracks
                .Select(x => x.Album)
                .Distinct().Count() == 1;

            if (folderIsAlbum)
            {
                _logger.LogDebug("Indexing {path} as album.", directoryGroup.Key);
                await IndexAlbum(analyzedTracks, library);
            }
            else
            {
                _logger.LogDebug("Indexing {path} as single files.", directoryGroup.Key);
                await IndexSingleFiles(analyzedTracks, library);
            }
            _logger.LogInformation("Completed indexing of {path}", directoryGroup.Key);
            // the change tracker consumes a lot of memory and 
            // progressively slows down the indexing process after a few 1000 items
            // so here it's being cleared after each folder
            // _context.ChangeTracker.Clear();
        }
        library.LastScan = DateTime.UtcNow;
        _context.SaveChanges();
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
                catch (IOException ex)
                {
                    _logger.LogError("Failed to read {File} due to IOException: {Exception} - retrying...", file.FullName, ex.ToString());
                    retries++;
                    await Task.Delay(1000);
                }
            }

            if (retries == maxRetries)
            {
                _logger.LogError("Failed to read track {Track} due to I/O errors.", file.FullName);
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
            var indexedAlbum = GetAlbum(artists, atlTrack);
            var indexedGenre = GetGenre(atlTrack.Genre);
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
            var indexedGenre = GetGenre(genre);
            createdGenres.Add(indexedGenre);
        }

        // most attributes are going to be the same in an album
        var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();

        var albumType = AlbumTypeHelper.GetAlbumType(distinctArtists.Where(a => a.Role == ArtistRole.Main).Count(), tracks.Count());
        var indexedAlbum = GetAlbum(distinctArtists, tracks.First());
        indexedAlbum.Type = albumType;
        foreach (var trackToIndex in tracks)
        {
            var targetGenre = createdGenres.FirstOrDefault(g => g.Name == trackToIndex.Genre);
            await IndexFile(artistForTracks[trackToIndex], indexedAlbum, targetGenre, trackToIndex, library);
        }
    }

    private async Task IndexFile(List<ArtistWithRole> artists, Album indexedAlbum, Genre? indexedGenre, ATL.Track atlTrack, MusicLibrary library)
    {
        var indexedTrack = _context.Tracks.Include(t => t.AudioFile).FirstOrDefault(t => t.AudioFile.FilePath == atlTrack.Path);
        if (indexedTrack != null)
        {
            return;
        }

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
                _logger.LogError("Failed to get artwork for track: {Track} due to the following exception: {Exception}", atlTrack.Path, ex.ToString());
            }
        }

        indexedTrack = new Track()
        {
            Album = indexedAlbum,
            Artists = artists,
            Title = !string.IsNullOrEmpty(atlTrack.Title) ? atlTrack.Title : Path.GetFileName(atlTrack.Path),
            Comment = atlTrack.Comment,
            Genre = indexedGenre,
            DiscNumber = atlTrack.DiscNumber,
            TrackNumber = atlTrack.TrackNumber,
            DurationInSeconds = atlTrack.Duration,
            Keywords = new List<Keyword>(),
            AudioFile = new AudioFile()
            {
                FilePath = atlTrack.Path,
                DateModified = File.GetLastWriteTimeUtc(atlTrack.Path),
                FileSizeInBytes = new FileInfo(atlTrack.Path).Length,
                AudioMetadata = new AudioMetadata()
                {
                    Bitrate = atlTrack.Bitrate,
                    Channels = atlTrack.ChannelsArrangement?.NbChannels,
                    SampleRate = atlTrack.SampleRate,
                    Codec = atlTrack.AudioFormat.ShortName,
                    BitDepth = atlTrack.BitDepth != -1 ? atlTrack.BitDepth : null,
                },
                Library = library
            }
        };
        _logger.LogDebug("Indexing track: {trackPath}", atlTrack.Path);
        _context.Tracks.Add(indexedTrack);
        await _searchService.InsertKeywordsForTrack(indexedTrack);
    }

    private Genre GetGenre(string genreName)
    {
        var indexedGenre = _context.Genres.FirstOrDefault(g => g.Name == genreName);
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
            _logger.LogDebug("Creating new artist: {artist}", artistName);
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
            var artistWithRole = _context.ArtistsWithRoles.FirstOrDefault(a => a.ArtistId == indexedArtist.Id && a.Role == role);
            if (artistWithRole == null)
            {
                artistWithRole = new ArtistWithRole()
                {
                    Artist = indexedArtist,
                    Role = role
                };
            }
            artistsWithRoles.Add(artistWithRole);
        }

        return artistsWithRoles;
    }

    private async Task<List<ArtistWithRole>> ParseArtists(string artist, string title)
    {
        var parsedFeaturingArtists = ParseFeaturingArtists(title);
        var parsedRemixers = ParseRemixers(title);

        var guestArtists = !string.IsNullOrEmpty(parsedFeaturingArtists) ? await GetArtistWithRole(SplitArtist(parsedFeaturingArtists), ArtistRole.Guest) : new List<ArtistWithRole>();
        var remixers = !string.IsNullOrEmpty(parsedRemixers) ? await GetArtistWithRole(SplitArtist(parsedRemixers), ArtistRole.Remixer) : new List<ArtistWithRole>();
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
        var parsedRemixers = remixerMatch.LastOrDefault()?.Value?.Trim();
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

    private Album GetAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack)
    {
        var albumName = !string.IsNullOrEmpty(atlTrack.Album) ? atlTrack.Album : Directory.GetParent(atlTrack.Path)?.Name;
        // Albums can have the same name, so in order to differentiate between them
        // we also use supplemental metadata. 
        var albumQuery = _context.Albums
            .Include(a => a.Artists)
            .Include(a => a.Tracks)
            .Where(a => a.Name == albumName && a.ReleaseYear == atlTrack.Year && a.DiscTotal == atlTrack.DiscTotal && a.TrackTotal == atlTrack.TrackTotal);
        var indexedAlbum = albumQuery.FirstOrDefault();
        if (indexedAlbum == null)
        {
            indexedAlbum = CreateAlbum(artists, atlTrack, albumName);
        }

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

    private Album CreateAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack, string? albumName)
    {

        var album = new Album()
        {
            Artists = new List<ArtistWithRole>(),
            Name = albumName!,
            ReleaseYear = atlTrack.Year,
            DiscTotal = atlTrack.DiscTotal,
            TrackTotal = atlTrack.TrackTotal,
            DateIndexed = DateTime.UtcNow,
            Artworks = new List<Artwork>()
        };

        _context.Albums.Add(album);
        album.Artists.AddRange(artists);
        return album;
    }
}