using Coral.Dto.EncodingModels;
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
    private readonly ITranscoderService _transcoderService;

    public TranscodeController(ILibraryService libraryService, ITranscoderService transcoderService)
    {
        _libraryService = libraryService;
        _transcoderService = transcoderService;
    }

    [HttpGet]
    [Route("tracks/{trackId}")]
    public async Task<ActionResult<StreamDto>> TranscodeTrack(int trackId)
    {
        var dbTrack = await _libraryService.GetTrack(trackId);
        if (dbTrack == null)
        {
            return NotFound(new
            {
                Message = "Track not found."
            });
        }

        var job = await _transcoderService.CreateJob(OutputFormat.AAC, opt =>
        {
            opt.SourceTrack = dbTrack;
            opt.Bitrate = 256;
            opt.RequestType = TranscodeRequestType.HLS;
        });

        var artworkPath = await _libraryService.GetArtworkForTrack(trackId);
        var streamData = new StreamDto()
        {
            Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
            TranscodeInfo = new TranscodeInfoDto()
            {
                JobId = job.Id,
                Bitrate = 256,
                Format = OutputFormat.AAC
            }
        };

        if (!string.IsNullOrEmpty(artworkPath))
        {
            // generate this url programmatically
            streamData.ArtworkUrl = Url.Action("GetTrackArtwork",
                "Repository",
                new {trackId = trackId},
                Request.Scheme);
        }

        return streamData;
    }
}