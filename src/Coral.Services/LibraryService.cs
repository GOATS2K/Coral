using ATL;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using Track = Coral.Database.Models.Track;

namespace Coral.Services
{
    public interface ILibraryService
    {
        public Task<TrackStream> GetStreamForTrack(int trackId);
        public Task<Track?> GetTrack(int trackId);
        public Task<TrackDto?> GetTrackDto(int trackId);
        public IAsyncEnumerable<TrackDto> GetTracks();
        public Task<ArtistDto?> GetArtist(int artistId);
        public IAsyncEnumerable<SimpleAlbumDto> GetAlbums();
        public Task<string?> GetArtworkForTrack(int trackId);
        public Task<string?> GetArtworkForAlbum(int albumId);
        public Task<AlbumDto?> GetAlbum(int albumId);
    }

    public class LibraryService : ILibraryService
    {
        private readonly CoralDbContext _context;
        private readonly IMapper _mapper;

        public LibraryService(CoralDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<Track?> GetTrack(int trackId)
        {
            return await _context.Tracks.FindAsync(trackId);
        }

        public async Task<TrackDto?> GetTrackDto(int trackId)
        {
            return await _context.Tracks.Where(t => t.Id == trackId)
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();
        }

        public async Task<TrackStream> GetStreamForTrack(int trackId)
        {
            var track = await _context.Tracks.FindAsync(trackId);
            if (track == null)
            {
                throw new ArgumentException($"Track ID {trackId} not found.");
            }

            var fileStream = new FileStream(track.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var trackStream = new TrackStream()
            {
                FileName = Path.GetFileName(track.FilePath),
                Length = new FileInfo(track.FilePath).Length,
                Stream = fileStream,
                ContentType = MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(track.FilePath))
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

        public async Task<ArtistDto?> GetArtist(int artistId)
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
                FeaturedIn = featured,
                InCompilation = compilations,
                Releases = mainReleases,
                RemixerIn = remixer
            };
        }

        public async Task<string?> GetArtworkForTrack(int trackId)
        {
            var track = await _context.Tracks.Include(t => t.Album)
                .FirstOrDefaultAsync(t => t.Id == trackId);

            return track?.Album?.CoverFilePath;
        }

        public async Task<string?> GetArtworkForAlbum(int albumId)
        {
            var album = await _context.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId);
            return album?.CoverFilePath;
        }

        public async Task<AlbumDto?> GetAlbum(int albumId)
        {
            var album = await _context.Albums
                .ProjectTo<AlbumDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(a => a.Id == albumId);

            if (album == null)
            {
                return null;
            }

            album.Tracks = album.Tracks.OrderBy(a => a.TrackNumber).ToList();
            return album;
        }
    }
}
