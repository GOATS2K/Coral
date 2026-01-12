using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly ILibraryService _libraryService;
        private readonly IFileSystemService _fileSystemService;

        public OnboardingController(ILibraryService libraryService, IFileSystemService fileSystemService)
        {
            _libraryService = libraryService;
            _fileSystemService = fileSystemService;
        }

        [HttpGet]
        [Route("root-directories")]
        public ActionResult<List<string>> RootDirectories()
        {
            return Ok(_fileSystemService.GetRootDirectories());
        }

        [HttpGet]
        [Route("list-directories")]
        public ActionResult<List<string>> DirectoriesInPath([FromQuery] string path)
        {
            return Ok(_fileSystemService.GetDirectoriesInPath(path));
        }

        [HttpGet]
        [Route("music-libraries")]
        public async Task<ActionResult<List<MusicLibraryDto>>> MusicLibraries()
        {
            return Ok(await _libraryService.GetMusicLibraries());
        }

        [HttpPost]
        [Route("register")]
        public async Task<ActionResult<MusicLibrary>> RegisterMusicLibrary([FromQuery] string path)
        {
            var library = await _libraryService.AddMusicLibrary(path);
            return library != null ? Ok(library) : BadRequest(new { Message = "Failed to register library" });
        }
    }
}
