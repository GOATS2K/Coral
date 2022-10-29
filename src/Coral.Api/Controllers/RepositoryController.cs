using Coral.Database.Models;
using Coral.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;

        public RepositoryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [HttpGet]
        [Route("tracks")]
        public async IAsyncEnumerable<Track> GetTracks()
        {
            await foreach (var track in _libraryService.GetTracks())
            {
                yield return track;
            }
        }
    }
}
