using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/artists")]
[ApiController]
[Authorize]
public class ArtistsController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly IPaginationService _paginationService;

    public ArtistsController(
        ILibraryService libraryService,
        IPaginationService paginationService)
    {
        _libraryService = libraryService;
        _paginationService = paginationService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedQuery<SimpleArtistDto>>> Artists(
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0)
    {
        var result = await _paginationService.PaginateQuery<Artist, SimpleArtistDto>(offset, limit);
        return Ok(result);
    }

    [HttpGet]
    [Route("{artistId}")]
    public async Task<ActionResult<ArtistDto>> Artist(Guid artistId)
    {
        var artist = await _libraryService.GetArtist(artistId);
        return artist != null ? Ok(artist) : NotFound();
    }
}
