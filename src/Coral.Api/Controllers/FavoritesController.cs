using Coral.Dto.Models;
using Coral.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/favorites")]
[ApiController]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoritesService _favoritesService;
    private readonly IPlaylistService _playlistService;

    public FavoritesController(
        IFavoritesService favoritesService,
        IPlaylistService playlistService)
    {
        _favoritesService = favoritesService;
        _playlistService = playlistService;
    }

    // Track favorites

    [HttpGet]
    [Route("tracks")]
    public async Task<ActionResult<PlaylistDto>> FavoriteTracks()
    {
        var playlist = await _favoritesService.GetAllTracks();
        return Ok(playlist);
    }

    [HttpPost]
    [Route("tracks/{trackId}")]
    public async Task<ActionResult> FavoriteTrack(Guid trackId)
    {
        try
        {
            await _favoritesService.AddTrack(trackId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    [HttpDelete]
    [Route("tracks/{trackId}")]
    public async Task<ActionResult> RemoveFavoriteTrack(Guid trackId)
    {
        try
        {
            await _favoritesService.RemoveTrack(trackId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    [HttpPut]
    [Route("tracks/{playlistTrackId}/reorder")]
    public async Task<ActionResult> ReorderFavoriteTrack(Guid playlistTrackId, [FromBody] int newPosition)
    {
        try
        {
            await _playlistService.ReorderTrack(playlistTrackId, newPosition);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    // Album favorites

    [HttpGet]
    [Route("albums")]
    public async Task<ActionResult<List<SimpleAlbumDto>>> FavoriteAlbums()
    {
        var albums = await _favoritesService.GetAllAlbums();
        return Ok(albums);
    }

    [HttpPost]
    [Route("albums/{albumId}")]
    public async Task<ActionResult> FavoriteAlbum(Guid albumId)
    {
        try
        {
            await _favoritesService.AddAlbum(albumId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    [HttpDelete]
    [Route("albums/{albumId}")]
    public async Task<ActionResult> RemoveFavoriteAlbum(Guid albumId)
    {
        try
        {
            await _favoritesService.RemoveAlbum(albumId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    // Artist favorites

    [HttpGet]
    [Route("artists")]
    public async Task<ActionResult<List<SimpleArtistDto>>> FavoriteArtists()
    {
        var artists = await _favoritesService.GetAllArtists();
        return Ok(artists);
    }

    [HttpPost]
    [Route("artists/{artistId}")]
    public async Task<ActionResult> FavoriteArtist(Guid artistId)
    {
        try
        {
            await _favoritesService.AddArtist(artistId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }

    [HttpDelete]
    [Route("artists/{artistId}")]
    public async Task<ActionResult> RemoveFavoriteArtist(Guid artistId)
    {
        try
        {
            await _favoritesService.RemoveArtist(artistId);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex);
        }
    }
}
