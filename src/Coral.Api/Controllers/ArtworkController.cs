using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ArtworkController : ControllerBase
{
    private readonly IArtworkService _artworkService;
    public ArtworkController(IArtworkService artworkService)
    {
        _artworkService = artworkService;
    }

    [HttpGet]
    [Route("{artworkId}")]
    public async Task<ActionResult> ArtworkFromId(int artworkId)
    {
        var artworkPath = await _artworkService.GetArtworkPath(artworkId);
        if (artworkPath == null) return NotFound();
        return new PhysicalFileResult(artworkPath,
            MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
    }
    
    [HttpGet]
    [Route("albums/{albumId}")]
    public async Task<ActionResult<ArtworkDto>> AlbumArtwork(int albumId)
    {
        var thumbnails = await _artworkService.GetArtworkForAlbum(albumId);
        if (thumbnails == null)
        {
            return NotFound();
        }

        return Ok(thumbnails);
    }
}