using Coral.Database;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Coral.Services;

public interface IIndexerService
{
    public Task ReadDirectory(string directory);
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<IndexerService> _logger;
    private static readonly string[] AudioFileFormats = { ".flac", ".mp3", ".wav", ".m4a", ".ogg", ".alac" };
    private static readonly string[] ImageFileFormats = { ".jpg", ".png" };
    private static readonly string[] ImageFileNames = { "cover", "artwork", "folder", "front" };

    public IndexerService(CoralDbContext context, ISearchService searchService, ILogger<IndexerService> logger, IArtworkService artworkService)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
    }

    private bool ContentDirectoryNeedsRescan(DirectoryInfo contentDirectory)
    {
        try
        {
            var maxValue = _context.Tracks.Max(t => t.DateModified);
            var contentsLastModified = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)))
                .Max(x => x.LastWriteTimeUtc);

            var contentsCreated = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)))
                .Max(x => x.CreationTimeUtc);

            return maxValue < contentsLastModified || maxValue < contentsCreated;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    public async Task ReadDirectory(string directory)
    {
        var contentDirectory = new DirectoryInfo(directory);
        if (!contentDirectory.Exists)
        {
            throw new ApplicationException("Content directory does not exist.");
        }

        if (!ContentDirectoryNeedsRescan(contentDirectory)) return;

        var directoryGroups = contentDirectory.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)))
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

            // we generally shouldn't be introducing side-effects in linq
            // but it's a lot prettier this way ;_;
            var analyzedTracks = tracksInDirectory.Select(x => new ATL.Track(x.FullName)).ToList();
            bool folderIsAlbum = analyzedTracks
                .Where(x => !string.IsNullOrEmpty(x.Album))
                .Select(x => x.Album)
                .Distinct().Count() == 1;

            if (folderIsAlbum)
            {
                _logger.LogInformation("Indexing {path} as album.", directoryGroup.Key);
                try
                {
                    await IndexAlbum(analyzedTracks);
                } catch (ArgumentException)
                {
                    _logger.LogError("Path contained tracks from another album, switching indexing method.");
                    await IndexSingleFiles(analyzedTracks);
                }
            }
            else
            {
                _logger.LogInformation("Indexing {path} as single files.", directoryGroup.Key);
                await IndexSingleFiles(analyzedTracks);
            }
            _logger.LogInformation("Completed indexing of {path}", directoryGroup.Key);
        }
    }

    private async Task IndexSingleFiles(List<ATL.Track> tracks)
    {
        foreach (var atlTrack in tracks)
        {
            var artists = ParseArtists(atlTrack.Artist, atlTrack.Title);
            var indexedAlbum = GetAlbum(artists.Select(a => a.Artist).ToList(), atlTrack);
            // set artists with roles on album
            indexedAlbum.ArtistsWithRoles.AddRange(artists);
            var indexedGenre = GetGenre(atlTrack.Genre);
            await IndexFile(artists, indexedAlbum, indexedGenre, atlTrack);
        }
    }

    private async Task IndexAlbum(List<ATL.Track> tracks)
    {
        // verify that the collection is not empty
        if (!tracks.Any())
        {
            throw new ArgumentOutOfRangeException("The track collection cannot be empty.");
        }

        // verify that we in fact have an album
        if (tracks.Select(t => t.Album).Distinct().Count() > 1)
        {
            throw new ArgumentException("The tracks are not from the same album.");
        }

        var distinctGenres = tracks.Select(t => t.Genre).Distinct();
        var createdGenres = new List<Genre>();
        
        // parse all artists
        var artistForTracks = new Dictionary<ATL.Track, List<ArtistWithRole>>();
        foreach (var track in tracks)
        {
            var artists = ParseArtists(track.Artist, track.Title);
            artistForTracks.Add(track, artists);
        }

        foreach (var genre in distinctGenres)
        {
            var indexedGenre = GetGenre(genre);
            if (indexedGenre == null)
            {
                continue;
            }
            createdGenres.Add(indexedGenre);
        }

        // most attributes are going to be the same in an album
        var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();
        var indexedAlbum = GetAlbum(distinctArtists.Select(a => a.Artist).ToList(), tracks.First());
        foreach (var trackToIndex in tracks)
        {
            indexedAlbum.ArtistsWithRoles.AddRange(artistForTracks[trackToIndex]);
            var targetGenre = createdGenres.SingleOrDefault(g => g.Name == trackToIndex.Genre);
            await IndexFile(artistForTracks[trackToIndex], indexedAlbum, targetGenre, trackToIndex);
        }
    }
    
    private async Task IndexFile(List<ArtistWithRole> artists, Album indexedAlbum, Genre? indexedGenre, ATL.Track atlTrack)
    {
        var indexedTrack = _context.Tracks.FirstOrDefault(t => t.FilePath == atlTrack.Path);
        if (indexedTrack != null)
        {
            return;
        }

        if (!indexedAlbum.Artworks.Any())
        {
            var albumArtwork = await GetAlbumArtwork(atlTrack);
            if (albumArtwork != null) await _artworkService.ProcessArtwork(indexedAlbum, albumArtwork);
        }

        indexedTrack = new Track()
        {
            Album = indexedAlbum,
            Artists = artists,
            Title = !string.IsNullOrEmpty(atlTrack.Title) ? atlTrack.Title : Path.GetFileName(atlTrack.Path),
            Comment = atlTrack.Comment,
            Genre = indexedGenre,
            DateModified = File.GetLastWriteTimeUtc(atlTrack.Path),
            DiscNumber = atlTrack.DiscNumber,
            TrackNumber = atlTrack.TrackNumber,
            DurationInSeconds = atlTrack.Duration,
            FilePath = atlTrack.Path,
            Keywords = new List<Keyword>()
        };
        _logger.LogInformation("Indexing track: {trackPath}", atlTrack.Path);
        _context.Tracks.Add(indexedTrack);
        await _searchService.InsertKeywordsForTrack(indexedTrack);
    }

    private Genre? GetGenre(string? genreName)
    {
        if (genreName == null) return null;
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

    private Artist GetArtist(string artistName)
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
            _logger.LogInformation("Creating new artist: {artist}", artistName);
            _context.SaveChanges();
        }
        return indexedArtist;
    }

    private List<string> SplitArtist(string? artistName)
    {
        if (artistName == null) return new List<string>();
        var split = artistName.Split(new char[] { ',', '&', ';', 'x'});
        return split.Select(s => s.Trim()).Distinct().ToList();
    }

    private List<ArtistWithRole> GetArtistWithRole(List<string> artists, ArtistRole role)
    {
        var artistsWithRoles = new List<ArtistWithRole>();
        foreach (var artist in artists)
        {
            var indexedArtist = GetArtist(artist.Trim());
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

    private List<ArtistWithRole> ParseArtists(string artist, string title)
    {
        var featuringRegex = @"\([fF](?:ea)?t(?:uring)?\.? (.*?)\)";
        var featuringMatch = Regex.Match(title, featuringRegex);
        var parsedFeaturingArtists = featuringMatch.Groups.Values.LastOrDefault()?.Value.Trim();

        // supports both (artist remix) and [artist remix]
        var remixerRegex = @"\(([^()]*)(?: Edit| Remix| VIP| Bootleg)\)|\[([^[\[\]]*)(?: Edit| Remix| VIP| Bootleg)\]";
        var remixerMatch = Regex.Match(title, remixerRegex, RegexOptions.IgnoreCase).Groups.Values.Where(a => !string.IsNullOrEmpty(a.Value));
        // first group is parenthesis, second is brackets
        var parsedRemixers = remixerMatch.LastOrDefault()?.Value?.Trim();

        var guestArtists = !string.IsNullOrEmpty(parsedFeaturingArtists) ? GetArtistWithRole(SplitArtist(parsedFeaturingArtists), ArtistRole.Guest) : new List<ArtistWithRole>();
        var remixers = !string.IsNullOrEmpty(parsedFeaturingArtists) ? GetArtistWithRole(SplitArtist(parsedRemixers), ArtistRole.Remixer) : new List<ArtistWithRole>();
        var mainArtists = GetArtistWithRole(SplitArtist(artist), ArtistRole.Main);

        var artistList = new List<ArtistWithRole>();
        artistList.AddRange(guestArtists);
        artistList.AddRange(mainArtists);
        artistList.AddRange(remixers);

        return artistList;
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

    private Album GetAlbum(List<Artist> artists, ATL.Track atlTrack)
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
                .OrderBy(a => a.Name)
                .SequenceEqual(artists.OrderBy(a => a.Name)))
        {
            var missingArtists = artists.Where(a => !indexedAlbum.Artists.Contains(a));
            indexedAlbum.Artists.AddRange(missingArtists);
            _context.Albums.Update(indexedAlbum);
        }
        return indexedAlbum;
    }

    private Album CreateAlbum(List<Artist> artists, ATL.Track atlTrack, string? albumName)
    {
        
        var album = new Album()
        {
            Artists = artists,
            ArtistsWithRoles = new List<ArtistWithRole>(),
            Name = albumName!,
            ReleaseYear = atlTrack.Year,
            DiscTotal = atlTrack.DiscTotal,
            TrackTotal = atlTrack.TrackTotal,
            DateIndexed = DateTime.UtcNow,
            Artworks = new List<Artwork>()
        };
        
        _context.Albums.Add(album);
        return album;
    }
}