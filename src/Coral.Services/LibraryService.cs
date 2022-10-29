using Coral.Database;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Dto.Models;

namespace Coral.Services
{
    public interface ILibraryService
    {
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
