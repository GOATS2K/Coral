using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;
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
        private readonly ILogger<LibraryService> _logger;

        public LibraryService(CoralDbContext context, IMapper mapper, ILogger<LibraryService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
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

        public IAsyncEnumerable<SimpleAlbumDto> GetAlbums()
        {
            return _context
                .Albums
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
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

            var featured = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Guest))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            var remixer = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Remixer))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            var compilations = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Main) && a.Type == AlbumType.Compilation)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

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

            album.Tracks = album.Tracks.OrderBy(t => t.DiscNumber).ThenBy(a => a.TrackNumber).ToList();
            return album;
        }

        public async Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId)
        {
            var trackEmbeddings = await _context.TrackEmbeddings.FirstOrDefaultAsync(t => t.TrackId == trackId);
            if (trackEmbeddings == null)
                return [];

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Set HNSW ef_search parameter to allow exploring more candidates during search
            // Default is 40, which limits results. Setting to 100 allows full result set.
            await _context.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.ef_search = 100");

            var recs = await _context.TrackEmbeddings
                .Select(t => new {Entity = t, Distance = t.Embedding.CosineDistance(trackEmbeddings.Embedding)} )
                .OrderBy(t => t.Distance)
                .Take(100)
                .ToListAsync();

            await transaction.CommitAsync();

            var trackIds = recs
                .Where(t => t.Distance < 0.2)
                // tracks with identical distance are duplicates
                .DistinctBy(t => t.Distance)
                .Select(t => t.Entity.TrackId)
                .Distinct().ToList();

            var tracks = await _context.Tracks
                .Where(t => trackIds.Contains(t.Id))
                .ProjectTo<SimpleTrackDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            List<SimpleTrackDto> orderedTracks = [];
            foreach (var track in recs)
            {
                var targetTrack = tracks.FirstOrDefault(t => t.Id == track.Entity.TrackId);
                if (targetTrack == null)
                    continue;
                orderedTracks.Add(targetTrack);
            }


            return orderedTracks.DistinctBy(t => t.Title).ToList();
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

                _logger.LogInformation("Added music library: {Path}", path);

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
