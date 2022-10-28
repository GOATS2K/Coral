using Coral.Database;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Services
{
    public interface ILibraryService
    {
        public IAsyncEnumerable<Track> GetTracks();
        public IAsyncEnumerable<Artist> GetArtist(string artistName);
    }

    public class LibraryService : ILibraryService
    {
        private readonly CoralDbContext _context;

        public LibraryService(CoralDbContext context)
        {
            _context = context;
        }

        public IAsyncEnumerable<Track> GetTracks()
        {
            return _context.Tracks.AsAsyncEnumerable();
        }

        public IAsyncEnumerable<Artist> GetArtist(string artistName)
        {
            return _context.Artists.Where(a => a.Name == artistName).AsAsyncEnumerable();
        }


    }
}
