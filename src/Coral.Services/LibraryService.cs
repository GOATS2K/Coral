using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Track = Coral.Database.Models.Track;

namespace Coral.Services
{
    public interface ILibraryService
    {
        public Task<TrackStream> GetStreamForTrack(Guid trackId);
        public Task<Track?> GetTrack(Guid trackId);
        public Task<TrackDto?> GetTrackDto(Guid trackId);
        public IAsyncEnumerable<TrackDto> GetTracks();
        public Task<ArtistDto?> GetArtist(Guid artistId);
        public IAsyncEnumerable<SimpleAlbumDto> GetAlbums();
        public Task<string?> GetArtworkForTrack(Guid trackId);
        public Task<string?> GetArtworkForAlbum(Guid albumId);
        public Task<AlbumDto?> GetAlbum(Guid albumId);
        public Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId);
        public Task<List<MusicLibraryDto>> GetMusicLibraries();
        public Task<MusicLibrary?> AddMusicLibrary(string path);
        public Task RemoveMusicLibrary(Guid libraryId);
        public Task<MusicLibrary?> GetMusicLibrary(Guid libraryId);
    }

    public class LibraryService : ILibraryService
    {
        private readonly CoralDbContext _context;
        private readonly IMapper _mapper;
        private readonly IScanChannel _scanChannel;
        private readonly ILogger<LibraryService> _logger;
        private readonly IEmbeddingService _embeddingService;
        private readonly IArtworkMappingHelper _artworkMappingHelper;

        public LibraryService(CoralDbContext context, IMapper mapper, IScanChannel scanChannel, ILogger<LibraryService> logger, IEmbeddingService embeddingService, IArtworkMappingHelper artworkMappingHelper)
        {
            _context = context;
            _mapper = mapper;
            _scanChannel = scanChannel;
            _logger = logger;
            _embeddingService = embeddingService;
            _artworkMappingHelper = artworkMappingHelper;
        }

        public async Task<Track?> GetTrack(Guid trackId)
        {
            return await _context.Tracks
                .Include(t => t.AudioFile)
                .ThenInclude(a => a.AudioMetadata)
                .FirstOrDefaultAsync(t => t.Id == trackId);
        }

        public async Task<TrackDto?> GetTrackDto(Guid trackId)
        {
            return await _context.Tracks.Where(t => t.Id == trackId)
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
        }

        public async Task<TrackStream> GetStreamForTrack(Guid trackId)
        {
            var track = await _context.Tracks.Include(t => t.AudioFile).FirstOrDefaultAsync(t => t.Id == trackId);
            if (track == null)
            {
                throw new ArgumentException($"Track ID {trackId} not found.");
            }

            var fileStream = new FileStream(track.AudioFile.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var trackStream = new TrackStream()
            {
                FileName = Path.GetFileName(track.AudioFile.FilePath),
                Length = new FileInfo(track.AudioFile.FilePath).Length,
                Stream = fileStream,
                ContentType = MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(track.AudioFile.FilePath))
            };

            return trackStream;
        }

        public IAsyncEnumerable<TrackDto> GetTracks()
        {
            return _context
                .Tracks
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public async IAsyncEnumerable<SimpleAlbumDto> GetAlbums()
        {
            var albums = await _context
                .Albums
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            await _artworkMappingHelper.MapArtworksToAlbums(albums);

            foreach (var album in albums)
            {
                yield return album;
            }
        }

        public async Task<ArtistDto?> GetArtist(Guid artistId)
        {
            //return await _context
            //    .Artists
            //    .Where(a => a.Id == artistId)
            //    .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider)
            //    .FirstOrDefaultAsync();

            var artist = await _context.Artists.Where(a => a.Id == artistId)
                .ProjectTo<SimpleArtistDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (artist == null) return null;

            var mainReleases = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Main) && a.Type != AlbumType.Compilation)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(mainReleases);

            var featured = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Guest))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(featured);

            var remixer = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Remixer))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(remixer);

            var compilations = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Main) && a.Type == AlbumType.Compilation)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(compilations);

            return new ArtistDto()
            {
                Id = artist.Id,
                Name = artist.Name,
                Favorited = artist.Favorited,
                FeaturedIn = featured,
                InCompilation = compilations,
                Releases = mainReleases,
                RemixerIn = remixer
            };
        }

        public async Task<string?> GetArtworkForTrack(Guid trackId)
        {
            var track = await _context.Tracks.Include(t => t.Album)
                .FirstOrDefaultAsync(t => t.Id == trackId);

            return track?.Album?.CoverFilePath;
        }

        public async Task<string?> GetArtworkForAlbum(Guid albumId)
        {
            var album = await _context.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId);
            return album?.CoverFilePath;
        }

        public async Task<AlbumDto?> GetAlbum(Guid albumId)
        {
            var album = await _context.Albums
                .ProjectTo<AlbumDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(a => a.Id == albumId);

            if (album == null)
            {
                return null;
            }

            await _artworkMappingHelper.MapArtworksToAlbums(album);

            album.Tracks = album.Tracks.OrderBy(t => t.DiscNumber).ThenBy(a => a.TrackNumber).ToList();
            return album;
        }

        public async Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId)
        {
            // Query DuckDB for similar tracks
            var similarTracks = await _embeddingService.GetSimilarTracksAsync(
                trackId,
                limit: 100,
                maxDistance: 0.2);

            if (!similarTracks.Any())
                return new List<SimpleTrackDto>();

            // Get track IDs (filtering by distance already done in EmbeddingService)
            var trackIds = similarTracks
                .DistinctBy(t => t.Distance)  // Identical distances = duplicates
                .Select(t => t.TrackId)
                .ToList();

            // Fetch full track info from SQLite
            var tracks = await _context.Tracks
                .Where(t => trackIds.Contains(t.Id))
                .ProjectTo<SimpleTrackDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            // Maintain order from similarity results
            var trackDict = tracks.ToDictionary(t => t.Id);
            var orderedTracks = trackIds
                .Where(id => trackDict.ContainsKey(id))
                .Select(id => trackDict[id])
                .ToList();

            return orderedTracks;
        }

        public async Task<List<MusicLibraryDto>> GetMusicLibraries()
        {
            return await _context
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

                // Queue initial scan after adding library
                var requestId = Guid.NewGuid().ToString();
                await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                    Library: library,
                    SpecificDirectory: null,
                    Incremental: false,
                    RequestId: requestId,
                    Trigger: ScanTrigger.LibraryAdded
                ));

                _logger.LogInformation("Library added and scan queued: {Path} (RequestId: {RequestId})", path, requestId);

                return library;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to add music library {Path}", path);
                return null;
            }
        }

        public async Task RemoveMusicLibrary(Guid libraryId)
        {
            var library = await _context.MusicLibraries.FindAsync(libraryId);
            if (library != null)
            {
                _context.MusicLibraries.Remove(library);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed music library {LibraryId} ({Path})", libraryId, library.LibraryPath);
            }
        }

        public async Task<MusicLibrary?> GetMusicLibrary(Guid libraryId)
        {
            return await _context.MusicLibraries.FindAsync(libraryId);
        }
    }
}
