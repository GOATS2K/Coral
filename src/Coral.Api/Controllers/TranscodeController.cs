using Coral.Dto.Models;
using Coral.Encoders;
using Coral.Encoders.EncodingModels;
using Coral.Services;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;

namespace Coral.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranscodeController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly ITranscodingJobManager _jobManager;

    public TranscodeController(ILibraryService libraryService, ITranscodingJobManager jobManager)
    {
        _libraryService = libraryService;
        _jobManager = jobManager;
    }

    [HttpGet]
    [Route("tracks/{trackId}")]
    public async Task<ActionResult<StreamDto>> TranscodeTrack(int trackId)
    {
        var dbTrack = await _libraryService.GetDatabaseTrack(trackId);
        if (dbTrack == null)
        {
            return NotFound(new
            {
                Message = "Track not found."
            });
        }

        var job = await _jobManager.CreateJob(OutputFormat.AAC, opt =>
        {
            opt.SourceTrack = dbTrack;
            opt.Bitrate = 256;
            opt.RequestType = TranscodeRequestType.HLS;
        });

        var artworkPath = await _libraryService.GetArtworkForTrack(trackId);
        var streamData = new StreamDto()
        {
            Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{Path.GetFileName(job.HlsPlaylistPath)}",
            RequestedBitrate = 256,
            RequestedFormat = OutputFormat.AAC
        };

        if (!string.IsNullOrEmpty(artworkPath))
        {
            // generate this url programmatically
            streamData.ArtworkUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/api/repository/artwork/{trackId}";
        }

        return streamData;
    }
}