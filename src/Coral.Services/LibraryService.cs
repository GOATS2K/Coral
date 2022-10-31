using Coral.Database;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;

namespace Coral.Services
{
    public interface ILibraryService
    {
        public Task<TrackStream> GetStreamForTrack(int trackId);
        public Task<Track?> GetDatabaseTrack(int trackId);
        public IAsyncEnumerable<TrackDto> GetTracks();
        public Task<List<ArtistDto>> GetArtist(string artistName);
        public IAsyncEnumerable<AlbumDto> GetAlbums();
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

        public async Task<Track?> GetDatabaseTrack(int trackId)
        {
            return await _context.Tracks.FindAsync(trackId);
        }

        public async Task<TrackStream> GetStreamForTrack(int trackId)
        {
            var track = await _context.Tracks.FindAsync(trackId);
            if (track == null)
            {
                throw new ArgumentException($"Track ID {trackId} not found.");
            }

            var fileStream = new FileStream(track.FilePath, FileMode.Open);
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

        public IAsyncEnumerable<AlbumDto> GetAlbums()
        {
            return _context
                .Albums
                .ProjectTo<AlbumDto>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public async Task<List<ArtistDto>> GetArtist(string artistName)
        {
            return await _context
                .Artists
                .Where(a => a.Name == artistName)
                .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }
    }
}
