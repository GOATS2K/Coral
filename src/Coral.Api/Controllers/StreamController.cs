using Coral.Configuration;
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
    private readonly ILogger<StreamController> _logger;

    private const int MaxRetries = 10;
    private const int RetryDelayMs = 50;

    private string GetRequestScheme()
    {
        if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-Proto", out var scheme) && scheme.Count > 0)
        {
            return scheme.ToString();
        }
        return HttpContext.Request.Scheme;
    }

    public StreamController(
        ILibraryService libraryService,
        ITranscoderService transcoderService,
        ISignedUrlService signedUrlService,
        ILogger<StreamController> logger)
    {
        _libraryService = libraryService;
        _transcoderService = transcoderService;
        _signedUrlService = signedUrlService;
        _logger = logger;
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
            Link = $"{GetRequestScheme()}://{HttpContext.Request.Host}/api/stream/hls/{job.Id}/{job.FinalOutputFile}",
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

        var streamData = new StreamDto()
        {
            Link = $"{GetRequestScheme()}://{HttpContext.Request.Host}/api/stream/hls/{job.Id}/{job.FinalOutputFile}",
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

    private static readonly HashSet<string> AllowedHlsExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".m3u8",
        ".m4s",
        ".ts"
    };

    /// <summary>
    /// Serves HLS files (manifests and segments) with retry logic for files being written.
    /// </summary>
    [HttpGet, HttpHead]
    [Route("hls/{jobId}/{fileName}")]
    [AllowAnonymous]
    public async Task<ActionResult> ServeHlsFile(Guid jobId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedHlsExtensions.Contains(extension))
        {
            return BadRequest();
        }

        var fullPath = Path.Combine(ApplicationConfiguration.HLSDirectory, jobId.ToString(), fileName);

        var contentType = extension.ToLowerInvariant() switch
        {
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".m4s" => "audio/mp4",
            ".ts" => "video/mp2t",
            _ => "application/octet-stream"
        };

        var attempt = 0;
        while (true)
        {
            try
            {
                var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                Response.Headers.Append("Cache-Control", "no-cache, no-store");
                return File(stream, contentType, enableRangeProcessing: true);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
            catch (IOException ex) when (attempt < MaxRetries)
            {
                attempt++;
                _logger.LogDebug(
                    "IOException reading HLS file {FilePath}, attempt {Attempt}/{MaxRetries}: {Message}",
                    fullPath,
                    attempt,
                    MaxRetries,
                    ex.Message);

                await Task.Delay(RetryDelayMs);
            }
        }
    }
}
