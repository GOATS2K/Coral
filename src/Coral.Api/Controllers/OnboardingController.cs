using Coral.Api.Workers;
using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database.Models;
using Coral.Dto;
using Coral.Dto.Models;
using Coral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    public record SystemInfoDto(int CpuCores);
    public record InferenceConfigRequest(int MaxConcurrentInstances);

    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly ILibraryService _libraryService;
        private readonly IFileSystemService _fileSystemService;
        private readonly EmbeddingWorker _embeddingWorker;

        public OnboardingController(
            ILibraryService libraryService,
            IFileSystemService fileSystemService,
            EmbeddingWorker embeddingWorker)
        {
            _libraryService = libraryService;
            _fileSystemService = fileSystemService;
            _embeddingWorker = embeddingWorker;
        }

        [HttpGet("system-info")]
        public ActionResult<SystemInfoDto> GetSystemInfo()
        {
            return Ok(new SystemInfoDto(Environment.ProcessorCount));
        }

        [HttpPost("configure-inference")]
        public IActionResult ConfigureInference([FromBody] InferenceConfigRequest request)
        {
            var maxCores = Environment.ProcessorCount;

            if (request.MaxConcurrentInstances < 1 || request.MaxConcurrentInstances > maxCores)
            {
                return BadRequest(new ApiError($"MaxConcurrentInstances must be between 1 and {maxCores}"));
            }

            var config = new ServerConfiguration();
            ApplicationConfiguration.GetConfiguration().Bind(config);
            config.Inference.MaxConcurrentInstances = request.MaxConcurrentInstances;
            ApplicationConfiguration.WriteConfiguration(config);

            // Synchronously update worker before returning to ensure correct concurrency
            // when library registration immediately follows this call
            _embeddingWorker.UpdateConcurrency(request.MaxConcurrentInstances);

            return Ok();
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
