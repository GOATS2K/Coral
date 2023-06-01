using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OnboardingController : ControllerBase
    {
        private readonly IIndexerService _indexerService;
        private readonly IFileSystemService _fileSystemService;

        public OnboardingController(IIndexerService indexerService, IFileSystemService fileSystemService)
        {
            _indexerService = indexerService;
            _fileSystemService = fileSystemService;
        }

        [HttpGet]
        [Route("listDirectories")]
        public ActionResult<List<string>> DirectoriesInPath([FromQuery] string path)
        {
            return Ok(_fileSystemService.GetDirectoriesInPath(path));
        }

        [HttpGet]
        [Route("musicLibraries")]
        public async Task<ActionResult<List<MusicLibraryDto>>> MusicLibraries()
        {
            return Ok(await _indexerService.GetMusicLibraries());
        }

        [HttpPost]
        [Route("register")]
        public async Task<ActionResult<MusicLibrary>> RegisterMusicLibrary([FromQuery] string path)
        {
            var library = await _indexerService.AddMusicLibrary(path);
            return library != null ? Ok(library) : BadRequest(new { Message = "Failed to register library" });
        }
    }
}
