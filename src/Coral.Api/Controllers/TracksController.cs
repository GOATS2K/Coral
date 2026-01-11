using Coral.Dto.Models;
using Coral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/tracks")]
[ApiController]
[Authorize]
public class TracksController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly IPlaybackService _playbackService;

    public TracksController(
        ILibraryService libraryService,
        IPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
    }

    [HttpGet]
    public async IAsyncEnumerable<TrackDto> Tracks()
    {
        await foreach (var track in _libraryService.GetTracks())
        {
            yield return track;
        }
    }

    [HttpPost]
    [Route("{trackId}/log-playback")]
    public async Task<IActionResult> LogPlayback(Guid trackId)
    {
        var track = await _libraryService.GetTrackDto(trackId);
        if (track == null)
            return NotFound();

        _playbackService.RegisterPlayback(track);
        return Ok();
    }

    [HttpGet]
    [Route("{trackId}/recommendations")]
    public async Task<ActionResult<List<SimpleTrackDto>>> RecommendationsForTrack(Guid trackId)
    {
        var tracks = await _libraryService.GetRecommendationsForTrack(trackId);
        if (tracks.Count == 0)
            return NotFound();
        return Ok(tracks);
    }
}
