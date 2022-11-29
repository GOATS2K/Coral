﻿using Coral.Dto.Models;
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
        [Route("artwork/track/{trackId}")]
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
    }
}
