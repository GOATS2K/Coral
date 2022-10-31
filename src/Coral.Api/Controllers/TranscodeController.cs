using Coral.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TranscodeController : ControllerBase
{
    private readonly ITranscoderService _transcoderService;
    private readonly ILibraryService _libraryService;

    public TranscodeController(ILibraryService libraryService, ITranscoderService transcoderService)
    {
        _libraryService = libraryService;
        _transcoderService = transcoderService;
    }
    
    // [HttpGet]
    // [Route("tracks/{trackId}")]
    // public async Task<IActionResult> TranscodeTrack(int trackId)
    // {
    //     var dbTrack = await _libraryService.GetDatabaseTrack(trackId);
    //     if (dbTrack == null)
    //     {
    //         return NotFound(new
    //         {
    //             Message = "Track not found."
    //         });
    //     }
    //
    //     var transcode = await _transcoderService.Transcode(dbTrack);
    //     return File(transcode.Stream, transcode.ContentType, fileDownloadName: transcode.FileName);
    // }
}