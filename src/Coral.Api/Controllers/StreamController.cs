using Coral.Dto.EncodingModels;
using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/stream")]
[ApiController]
[Authorize]
public class StreamController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly ITranscoderService _transcoderService;
    private readonly ISignedUrlService _signedUrlService;

    public StreamController(
        ILibraryService libraryService,
        ITranscoderService transcoderService,
        ISignedUrlService signedUrlService)
    {
        _libraryService = libraryService;
        _transcoderService = transcoderService;
        _signedUrlService = signedUrlService;
    }

    /// <summary>
    /// Serves the original audio file. Requires a valid signed URL.
    /// </summary>
    [HttpGet, HttpHead]
    [Route("{trackId}")]
    [AllowAnonymous]
    public async Task<ActionResult> StreamFile(
        Guid trackId,
        [FromQuery] long expires,
        [FromQuery] string signature)
    {
        if (string.IsNullOrEmpty(signature) || !_signedUrlService.ValidateSignature(trackId, expires, signature))
            return Unauthorized();

        try
        {
            var trackStream = await _libraryService.GetStreamForTrack(trackId);
            return File(trackStream.Stream, trackStream.ContentType, true);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Gets a signed URL for the original audio file.
    /// </summary>
    [HttpGet]
    [Route("tracks/{trackId}/original")]
    public async Task<ActionResult<StreamDto>> GetOriginalStreamUrl(Guid trackId)
    {
        var track = await _libraryService.GetTrack(trackId);
        if (track == null)
        {
            return NotFound(new { Message = "Track not found." });
        }

        var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/api/stream/{trackId}";
        var signedUrl = _signedUrlService.GenerateSignedUrl(trackId, baseUrl);

        return new StreamDto { Link = signedUrl };
    }

    /// <summary>
    /// Transcodes the track to AAC and returns an HLS playlist URL.
    /// </summary>
    [HttpGet]
    [Route("tracks/{trackId}/transcode")]
    public async Task<ActionResult<StreamDto>> TranscodeTrack(Guid trackId, int bitrate)
    {
        var dbTrack = await _libraryService.GetTrack(trackId);
        if (dbTrack == null)
        {
            return NotFound(new { Message = "Track not found." });
        }

        var job = await _transcoderService.CreateJob(OutputFormat.AAC, opt =>
        {
            opt.SourceTrack = dbTrack;
            opt.Bitrate = bitrate;
            opt.RequestType = TranscodeRequestType.HLS;
        });

        var streamData = new StreamDto()
        {
            Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
            TranscodeInfo = new TranscodeInfoDto()
            {
                JobId = job.Id,
                Bitrate = job.Request.Bitrate,
                Format = OutputFormat.AAC
            }
        };

        return streamData;
    }

    /// <summary>
    /// Remuxes the track to HLS without re-encoding and returns an HLS playlist URL.
    /// </summary>
    [HttpGet]
    [Route("tracks/{trackId}/hls")]
    public async Task<ActionResult<StreamDto>> StreamTrack(Guid trackId)
    {
        var dbTrack = await _libraryService.GetTrack(trackId);
        if (dbTrack == null)
        {
            return NotFound(new { Message = "Track not found." });
        }

        var job = await _transcoderService.CreateJob(OutputFormat.Remux, opt =>
        {
            opt.SourceTrack = dbTrack;
            opt.Bitrate = 0;
            opt.RequestType = TranscodeRequestType.HLS;
        });

        var ffprobeResult = await Ffprobe.GetAudioMetadata(dbTrack.AudioFile.FilePath);
        var audioStream = ffprobeResult?.Streams.FirstOrDefault(s => s.CodecType == "audio");
        var codec = audioStream?.CodecName;
        string targetScheme;
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var scheme))
        {
            targetScheme = scheme;
        }
        else
        {
            targetScheme = HttpContext.Request.Scheme;
        }

        var streamData = new StreamDto()
        {
            Link = $"{targetScheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
            TranscodeInfo = new TranscodeInfoDto()
            {
                JobId = job.Id,
                Bitrate = 0,
                Format = OutputFormat.Remux,
                Codec = codec
            }
        };

        return streamData;
    }
}
