using Coral.Database.Models;
using Coral.Dto.EncodingModels;
using Coral.Dto.Models;
using Coral.Events;
using Coral.Services;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Indexer;
using Coral.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Coral.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly ILibraryService _libraryService;
        private readonly ITranscoderService _transcoderService;
        private readonly ISearchService _searchService;
        private readonly IPaginationService _paginationService;
        private readonly IPlaybackService _playbackService;
        private readonly IFavoritesService _favoritesService;
        private readonly IScanChannel _scanChannel;
        private readonly IArtworkMappingHelper _artworkMappingHelper;

        public LibraryController(ILibraryService libraryService, ITranscoderService transcoderService,
            ISearchService searchService, IPaginationService paginationService,
            TrackPlaybackEventEmitter eventEmitter, IPlaybackService playbackService,
            IFavoritesService favoritesService, IScanChannel scanChannel, IArtworkMappingHelper artworkMappingHelper)
        {
            _libraryService = libraryService;
            _transcoderService = transcoderService;
            _searchService = searchService;
            _paginationService = paginationService;
            _playbackService = playbackService;
            _favoritesService = favoritesService;
            _scanChannel = scanChannel;
            _artworkMappingHelper = artworkMappingHelper;
        }

        [HttpPost]
        [Route("scan")]
        public async Task<ActionResult> RunIndexer()
        {
            var libraries = await _libraryService.GetMusicLibraries();
            foreach (var library in libraries)
            {
                var dbLibrary = await _libraryService.GetMusicLibrary(library.Id);
                if (dbLibrary != null)
                {
                    await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                        dbLibrary,
                        Trigger: ScanTrigger.Manual
                    ));
                }
            }
            return Ok();
        }

        [HttpGet]
        [Route("search")]
        public async Task<ActionResult<PaginatedCustomData<SearchResult>>> Search([FromQuery] string query,
            [FromQuery] int offset = 0, [FromQuery] int limit = 100)
        {
            var searchResult = await _searchService.Search(query, offset, limit);
            return Ok(searchResult);
        }

        [HttpGet]
        [Route("tracks/{trackId}/logPlayback")]
        public async Task<IActionResult> LogPlayback(Guid trackId)
        {
            var track = await _libraryService.GetTrackDto(trackId);
            if (track != null)
            {
                _playbackService.RegisterPlayback(track);
                return Ok();
            }

            return NotFound();
        }

        [HttpGet, HttpHead]
        [Route("tracks/{trackId}/original")]
        public async Task<ActionResult> FileFromLibrary(Guid trackId)
        {
            try
            {
                var trackStream = await _libraryService.GetStreamForTrack(trackId);
                return File(trackStream.Stream, trackStream.ContentType, true);
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
        public async Task<ActionResult<StreamDto>> TranscodeTrack(Guid trackId, int bitrate)
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
                opt.Bitrate = bitrate;
                opt.RequestType = TranscodeRequestType.HLS;
            });

            var streamData = new StreamDto()
            {
                // this will require some baseurl modifications via the web server
                // responsible for reverse proxying Coral
                Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
                TranscodeInfo = new TranscodeInfoDto()
                {
                    JobId = job.Id,
                    Bitrate = job.Request.Bitrate,
                    Format = OutputFormat.AAC
                }
            };

            return streamData;
        }

        [HttpGet]
        [Route("tracks/{trackId}/stream")]
        public async Task<ActionResult<StreamDto>> StreamTrack(Guid trackId)
        {
            var dbTrack = await _libraryService.GetTrack(trackId);
            if (dbTrack == null)
            {
                return NotFound(new
                {
                    Message = "Track not found."
                });
            }

            var job = await _transcoderService.CreateJob(OutputFormat.Remux, opt =>
            {
                opt.SourceTrack = dbTrack;
                opt.Bitrate = 0;  // Not used for remux
                opt.RequestType = TranscodeRequestType.HLS;
            });

            // Get accurate codec info from ffprobe
            var ffprobeResult = await Ffprobe.GetAudioMetadata(dbTrack.AudioFile.FilePath);
            var audioStream = ffprobeResult?.Streams.FirstOrDefault(s => s.CodecType == "audio");
            var codec = audioStream?.CodecName;

            var streamData = new StreamDto()
            {
                Link = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}/hls/{job.Id}/{job.FinalOutputFile}",
                TranscodeInfo = new TranscodeInfoDto()
                {
                    JobId = job.Id,
                    Bitrate = 0,  // Remux preserves original bitrate
                    Format = OutputFormat.Remux,
                    Codec = codec
                }
            };

            return streamData;
        }

        [HttpGet]
        [Route("tracks/{trackId}/recommendations")]
        public async Task<ActionResult<List<SimpleTrackDto>>> RecommendationsForTrack(Guid trackId)
        {
            var tracks = await _libraryService.GetRecommendationsForTrack(trackId);
            if (tracks.Count == 0)
                return NotFound();
            return Ok(tracks);
        }

        [HttpGet]
        [Route("tracks/favorites")]
        public async Task<ActionResult<List<SimpleTrackDto>>> FavoriteTracks()
        {
            var tracks = await _favoritesService.GetAllTracks();
            return Ok(tracks);
        }

        [HttpGet]
        [Route("/albums/favorites")]
        public async Task<ActionResult<List<SimpleAlbumDto>>> FavoriteAlbums()
        {
            var albums = await _favoritesService.GetAllAlbums();
            return Ok(albums);
        }

        [HttpGet]
        [Route("albums/recently-added")]
        public async Task<ActionResult<List<SimpleAlbumDto>>> RecentlyAddedAlbums()
        {
            var albums = await _libraryService.GetRecentlyAddedAlbums();
            return Ok(albums);
        }

        [HttpGet]
        [Route("artists/favorites")]
        public async Task<ActionResult<List<SimpleArtistDto>>> FavoriteArtists()
        {
            var artists = await _favoritesService.GetAllArtists();
            return Ok(artists);
        }

        [HttpPost]
        [Route("tracks/{trackId}/favorite")]
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
        [Route("tracks/{trackId}/favorite")]
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

        [HttpPost]
        [Route("artists/{artistId}/favorite")]
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
        [Route("artists/{artistId}/favorite")]
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

        [HttpPost]
        [Route("albums/{albumId}/favorite")]
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
        [Route("albums/{albumId}/favorite")]
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

        [HttpGet]
        [Route("tracks")]
        public async IAsyncEnumerable<TrackDto> Tracks()
        {
            await foreach (var track in _libraryService.GetTracks())
            {
                yield return track;
            }
        }

        [HttpGet]
        [Route("albums")]
        public async IAsyncEnumerable<SimpleAlbumDto> Albums()
        {
            await foreach (var album in _libraryService.GetAlbums())
            {
                yield return album;
            }
        }

        [HttpGet]
        [Route("albums/paginated")]
        public async Task<ActionResult<PaginatedQuery<SimpleAlbumDto>>> PaginatedAlbums([FromQuery] int limit = 10,
            [FromQuery] int offset = 0)
        {
            var result = await _paginationService.PaginateQuery<Album, SimpleAlbumDto>(offset, limit);

            // Populate artworks for the albums
            await _artworkMappingHelper.MapArtworksToAlbums(result.Data);

            return Ok(result);
        }

        [HttpGet]
        [Route("artists/paginated")]
        public async Task<ActionResult<PaginatedQuery<SimpleArtistDto>>> PaginatedArtists([FromQuery] int limit = 10,
            [FromQuery] int offset = 0)
        {
            var result = await _paginationService.PaginateQuery<Artist, SimpleArtistDto>(offset, limit);
            return Ok(result);
        }

        [HttpGet]
        [Route("albums/{albumId}")]
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
        [Route("albums/{albumId}/tracks")]
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
        [Route("artists/{artistId}")]
        public async Task<ActionResult<ArtistDto>> Artist(Guid artistId)
        {
            var artist = await _libraryService.GetArtist(artistId);
            return artist != null ? Ok(artist) : NotFound();
        }
    }
}