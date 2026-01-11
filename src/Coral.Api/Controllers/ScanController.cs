using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Indexer;
using Coral.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/scan")]
[ApiController]
[Authorize]
public class ScanController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly IScanChannel _scanChannel;
    private readonly IScanReporter _scanReporter;

    public ScanController(
        ILibraryService libraryService,
        IScanChannel scanChannel,
        IScanReporter scanReporter)
    {
        _libraryService = libraryService;
        _scanChannel = scanChannel;
        _scanReporter = scanReporter;
    }

    [HttpPost]
    public async Task<ActionResult<ScanInitiatedDto>> RunIndexer()
    {
        var scans = new List<ScanRequestInfo>();
        var libraries = await _libraryService.GetMusicLibraries();

        foreach (var library in libraries)
        {
            var dbLibrary = await _libraryService.GetMusicLibrary(library.Id);
            if (dbLibrary == null) continue;
            var requestId = Guid.NewGuid();
            await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                dbLibrary,
                RequestId: requestId,
                Trigger: ScanTrigger.Manual
            ));

            scans.Add(new ScanRequestInfo
            {
                RequestId = requestId,
                LibraryId = library.Id,
                LibraryName = library.LibraryPath
            });
        }

        return Ok(new ScanInitiatedDto { Scans = scans });
    }

    [HttpGet]
    [Route("progress/{requestId}")]
    public ActionResult<ScanJobProgress> GetScanProgress(Guid requestId)
    {
        var progress = _scanReporter.GetProgress(requestId);
        if (progress == null)
        {
            return NotFound($"No active scan found with RequestId: {requestId}");
        }
        return Ok(progress);
    }

    [HttpGet]
    [Route("active")]
    public ActionResult<List<ScanJobProgress>> GetActiveScans()
    {
        var activeScans = _scanReporter.GetActiveScans();
        return Ok(activeScans);
    }
}
