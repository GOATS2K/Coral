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
        public Task<TrackStream> GetStreamForTrack(Guid trackId);
        public Task<Track?> GetTrack(Guid trackId);
        public IAsyncEnumerable<TrackDto> GetTracks();
        public Task<List<SimpleArtistDto>> GetArtist(string artistName);
        public IAsyncEnumerable<SimpleAlbumDto> GetAlbums();
        public Task<string?> GetArtworkForTrack(Guid trackId);
        public Task<string?> GetArtworkForAlbum(Guid albumId);
        public Task<AlbumDto?> GetAlbum(Guid albumId);
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

        public async Task<Track?> GetTrack(Guid trackId)
        {
            return await _context.Tracks.FindAsync(trackId);
        }
        
        public async Task<TrackStream> GetStreamForTrack(Guid trackId)
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

        public async Task<List<SimpleArtistDto>> GetArtist(string artistName)
        {
            return await _context
                .Artists
                .Where(a => a.Name == artistName)
                .ProjectTo<SimpleArtistDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
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

            album.Tracks = album.Tracks.OrderBy(a => a.TrackNumber).ToList();
            return album;
        }
    }
}
