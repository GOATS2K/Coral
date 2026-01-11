using Coral.Services;
using Coral.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/search")]
[ApiController]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedCustomData<SearchResult>>> Search(
        [FromQuery] string query,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100)
    {
        var searchResult = await _searchService.Search(query, offset, limit);
        return Ok(searchResult);
    }
}
