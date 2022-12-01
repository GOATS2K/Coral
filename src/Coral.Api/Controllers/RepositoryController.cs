using Coral.Dto.Models;
using Coral.Services;
using Coral.Services.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;

        public RepositoryController(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [HttpGet]
        [Route("tracks/{trackId}/stream")]
        public async Task<ActionResult> StreamTrack(int trackId)
        {
            try
            {
                var trackStream = await _libraryService.GetStreamForTrack(trackId);
                return File(trackStream.Stream, trackStream.ContentType);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new
                {
                    Message = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("tracks/{trackId}/artwork")]
        public async Task<ActionResult> GetTrackArtwork(int trackId)
        {
            var artworkPath = await _libraryService.GetArtworkForTrack(trackId);
            if (artworkPath == null)
            {
                return NotFound();
            }
            return new PhysicalFileResult(artworkPath, MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
        }

        [HttpGet]
        [Route("albums/{albumId}/artwork")]
        public async Task<ActionResult> GetAlbumArtwork(int albumId)
        {
            var artworkPath = await _libraryService.GetArtworkForAlbum(albumId);
            if (artworkPath == null)
            {
                return NotFound();
            }
            return new PhysicalFileResult(artworkPath, MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
        }

        [HttpGet]
        [Route("tracks")]
        public async IAsyncEnumerable<TrackDto> GetTracks()
        {
            await foreach (var track in _libraryService.GetTracks())
            {
                yield return track;
            }
        }

        [HttpGet]
        [Route("albums")]
        public async IAsyncEnumerable<AlbumDto> GetAlbums()
        {
            await foreach (var album in _libraryService.GetAlbums())
            {
                yield return album;
            }
        }

        [HttpGet]
        [Route("albums/{albumId}")]
        public async Task<ActionResult<AlbumDto>> GetAlbum(int albumId)
        {
            var album = await _libraryService.GetAlbum(albumId);
            if (album == null)
            {
                return NotFound();
            }
            return album;
        }


    }
}
