﻿using System.Net;
using Coral.Dto.EncodingModels;
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
        private readonly ITranscoderService _transcoderService;

        public RepositoryController(ILibraryService libraryService, ITranscoderService transcoderService)
        {
            _libraryService = libraryService;
            _transcoderService = transcoderService;
        }

        [HttpGet]
        [Route("tracks/{trackId}/original")]
        public async Task<ActionResult> GetFileFromLibrary(int trackId)
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
        [Route("tracks/{trackId}/transcode")]
        public async Task<ActionResult<StreamDto>> TranscodeTrack(int trackId)
        {
            var dbTrack = await _libraryService.GetTrack(trackId);
            if (dbTrack == null)
            {
                return NotFound(new
                {
                    Message = "Track not found."
                });
            }

            var job = await _transcoderService.CreateJob(OutputFormat.AAC, opt =>
            {
                opt.SourceTrack = dbTrack;
                opt.Bitrate = 256;
                opt.RequestType = TranscodeRequestType.HLS;
            });

            var artworkPath = await _libraryService.GetArtworkForTrack(trackId);
            var streamData = new StreamDto()
            {
                Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
                TranscodeInfo = new TranscodeInfoDto()
                {
                    JobId = job.Id,
                    Bitrate = 256,
                    Format = OutputFormat.AAC
                }
            };

            if (!string.IsNullOrEmpty(artworkPath))
            {
                // generate this url programmatically
                streamData.ArtworkUrl = Url.Action("GetTrackArtwork",
                    "Repository",
                    new {trackId = trackId},
                    Request.Scheme);
            }

            return streamData;
        }

        // [HttpGet]
        // [Route("tracks/{trackId}/stream")]
        // public async Task<ActionResult<StreamDto>> StreamTrack(int trackId,
        //     [FromQuery] int bitrate = 192,
        //     [FromQuery] bool transcodeTrack = true)
        // {
        //     if (!transcodeTrack)
        //     {
        //         return new StreamDto()
        //         {
        //             Link = Url.Action("GetFileFromLibrary", "Repository", new
        //             {
        //                 trackId = trackId
        //             })!
        //         };
        //     }
        //
        //     // get track
        //     
        //     // check if we should transcode
        //         // if lossy, return original
        //         // use requested bitrate
        //         
        //     // return StreamDto
        // }

        [HttpGet]
        [Route("tracks/{trackId}/artwork")]
        public async Task<ActionResult> GetTrackArtwork(int trackId)
        {
            var artworkPath = await _libraryService.GetArtworkForTrack(trackId);
            if (artworkPath == null)
            {
                return NotFound();
            }

            return new PhysicalFileResult(artworkPath,
                MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
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

            return new PhysicalFileResult(artworkPath,
                MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(artworkPath)));
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