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
    public async Task<ActionResult> ArtworkFromId(Guid artworkId)
    {
        var artworkPath = await _artworkService.GetArtworkPath(artworkId);
        if (artworkPath == null) return NotFound();
        return new PhysicalFileResult(artworkPath,
            MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
    }
}