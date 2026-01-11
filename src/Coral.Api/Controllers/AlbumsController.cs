using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Coral.Api.Controllers;

[Route("api/albums")]
[ApiController]
[Authorize]
public class AlbumsController : ControllerBase
{
    private readonly ILibraryService _libraryService;
    private readonly IPaginationService _paginationService;
    private readonly IArtworkMappingHelper _artworkMappingHelper;
    private readonly IMemoryCache _memoryCache;

    public AlbumsController(
        ILibraryService libraryService,
        IPaginationService paginationService,
        IArtworkMappingHelper artworkMappingHelper,
        IMemoryCache memoryCache)
    {
        _libraryService = libraryService;
        _paginationService = paginationService;
        _artworkMappingHelper = artworkMappingHelper;
        _memoryCache = memoryCache;
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedQuery<SimpleAlbumDto>>> Albums(
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0)
    {
        var result = await _paginationService.PaginateQuery<Album, SimpleAlbumDto>(offset, limit);
        await _artworkMappingHelper.MapArtworksToAlbums(result.Data);
        return Ok(result);
    }

    [HttpGet]
    [Route("all")]
    public async IAsyncEnumerable<SimpleAlbumDto> AllAlbums()
    {
        await foreach (var album in _libraryService.GetAlbums())
        {
            yield return album;
        }
    }

    [HttpGet]
    [Route("recently-added")]
    public async Task<ActionResult<List<SimpleAlbumDto>>> RecentlyAddedAlbums()
    {
        var albums = await _libraryService.GetRecentlyAddedAlbums();
        return Ok(albums);
    }

    [HttpGet]
    [Route("{albumId}")]
    public async Task<ActionResult<AlbumDto>> Album(Guid albumId)
    {
        var album = await _libraryService.GetAlbum(albumId);
        if (album == null)
        {
            return NotFound();
        }

        return album;
    }

    [HttpGet]
    [Route("{albumId}/tracks")]
    public async Task<ActionResult<List<SimpleTrackDto>>> AlbumTracks(Guid albumId)
    {
        var album = await _libraryService.GetAlbum(albumId);
        if (album == null)
        {
            return NotFound();
        }

        return Ok(album.Tracks);
    }

    [HttpGet]
    [Route("{albumId}/recommendations")]
    public async Task<ActionResult<List<AlbumRecommendationDto>>> RecommendationsForAlbum(Guid albumId)
    {
        var cacheKey = $"album_recs:{albumId}";

        if (_memoryCache.TryGetValue<List<AlbumRecommendationDto>>(cacheKey, out var cachedRecommendations))
        {
            if (cachedRecommendations != null)
            {
                return Ok(cachedRecommendations);
            }
        }

        var recommendations = await _libraryService.GetRecommendationsForAlbum(albumId);

        if (recommendations.Count == 0)
            return NotFound();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2),
            Size = 1
        };

        _memoryCache.Set(cacheKey, recommendations, cacheOptions);

        return Ok(recommendations);
    }
}
