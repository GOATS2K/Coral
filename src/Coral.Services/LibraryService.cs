using AutoMapper;
using AutoMapper.QueryableExtensions;
using Coral.Database;
using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.ChannelWrappers;
using Coral.Services.Helpers;
using Coral.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Track = Coral.Database.Models.Track;

namespace Coral.Services
{
    public interface ILibraryService
    {
        public Task<TrackStream> GetStreamForTrack(Guid trackId);
        public Task<Track?> GetTrack(Guid trackId);
        public Task<TrackDto?> GetTrackDto(Guid trackId);
        public IAsyncEnumerable<TrackDto> GetTracks();
        public Task<ArtistDto?> GetArtist(Guid artistId);
        public IAsyncEnumerable<SimpleAlbumDto> GetAlbums();
        public Task<string?> GetArtworkForTrack(Guid trackId);
        public Task<string?> GetArtworkForAlbum(Guid albumId);
        public Task<AlbumDto?> GetAlbum(Guid albumId);
        public Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId);
        public Task<List<AlbumRecommendationDto>> GetRecommendationsForAlbum(Guid albumId);
        public Task<List<MusicLibraryDto>> GetMusicLibraries();
        public Task<MusicLibrary?> AddMusicLibrary(string path);
        public Task RemoveMusicLibrary(Guid libraryId);
        public Task<MusicLibrary?> GetMusicLibrary(Guid libraryId);
        Task<List<SimpleAlbumDto>> GetRecentlyAddedAlbums();
    }

    public class LibraryService : ILibraryService
    {
        private readonly CoralDbContext _context;
        private readonly IMapper _mapper;
        private readonly IScanChannel _scanChannel;
        private readonly ILogger<LibraryService> _logger;
        private readonly IEmbeddingService _embeddingService;
        private readonly IArtworkMappingHelper _artworkMappingHelper;
        private readonly IFavoritedMappingHelper _favoritedMappingHelper;

        public LibraryService(CoralDbContext context, IMapper mapper, IScanChannel scanChannel, ILogger<LibraryService> logger, IEmbeddingService embeddingService, IArtworkMappingHelper artworkMappingHelper, IFavoritedMappingHelper favoritedMappingHelper)
        {
            _context = context;
            _mapper = mapper;
            _scanChannel = scanChannel;
            _logger = logger;
            _embeddingService = embeddingService;
            _artworkMappingHelper = artworkMappingHelper;
            _favoritedMappingHelper = favoritedMappingHelper;
        }

        public async Task<List<SimpleAlbumDto>> GetRecentlyAddedAlbums()
        {
            var albums = await _context
                .Albums
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(albums);
            return albums;
        }

        public async Task<Track?> GetTrack(Guid trackId)
        {
            return await _context.Tracks
                .Include(t => t.AudioFile)
                .ThenInclude(a => a.AudioMetadata)
                .FirstOrDefaultAsync(t => t.Id == trackId);
        }

        public async Task<TrackDto?> GetTrackDto(Guid trackId)
        {
            var track = await _context.Tracks.Where(t => t.Id == trackId)
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (track != null)
            {
                await _favoritedMappingHelper.MapFavoritedToTracks(track);
            }

            return track;
        }

        public async Task<TrackStream> GetStreamForTrack(Guid trackId)
        {
            var track = await _context.Tracks.Include(t => t.AudioFile).FirstOrDefaultAsync(t => t.Id == trackId);
            if (track == null)
            {
                throw new ArgumentException($"Track ID {trackId} not found.");
            }

            var fileStream = new FileStream(track.AudioFile.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var trackStream = new TrackStream()
            {
                FileName = Path.GetFileName(track.AudioFile.FilePath),
                Length = new FileInfo(track.AudioFile.FilePath).Length,
                Stream = fileStream,
                ContentType = MimeTypeHelper.GetMimeTypeForExtension(Path.GetExtension(track.AudioFile.FilePath))
            };

            return trackStream;
        }

        public IAsyncEnumerable<TrackDto> GetTracks()
        {
            return _context
                .Tracks
                .ProjectTo<TrackDto>(_mapper.ConfigurationProvider)
                .AsAsyncEnumerable();
        }

        public async IAsyncEnumerable<SimpleAlbumDto> GetAlbums()
        {
            var albums = await _context
                .Albums
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            await _artworkMappingHelper.MapArtworksToAlbums(albums);

            foreach (var album in albums)
            {
                yield return album;
            }
        }

        public async Task<ArtistDto?> GetArtist(Guid artistId)
        {
            //return await _context
            //    .Artists
            //    .Where(a => a.Id == artistId)
            //    .ProjectTo<ArtistDto>(_mapper.ConfigurationProvider)
            //    .FirstOrDefaultAsync();

            var artist = await _context.Artists.Where(a => a.Id == artistId)
                .ProjectTo<SimpleArtistDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (artist == null) return null;

            var mainReleases = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Main) && a.Type != AlbumType.Compilation)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(mainReleases);

            var featured = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Guest))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(featured);

            var remixer = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Remixer))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(remixer);

            var compilations = await _context.Albums
                .Where(a => a.Artists.Any(albumArtist => albumArtist.ArtistId == artist.Id && albumArtist.Role == ArtistRole.Main) && a.Type == AlbumType.Compilation)
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
            await _artworkMappingHelper.MapArtworksToAlbums(compilations);

            return new ArtistDto()
            {
                Id = artist.Id,
                Name = artist.Name,
                Favorited = artist.Favorited,
                FeaturedIn = featured,
                InCompilation = compilations,
                Releases = mainReleases,
                RemixerIn = remixer
            };
        }

        public async Task<string?> GetArtworkForTrack(Guid trackId)
        {
            var track = await _context.Tracks.Include(t => t.Album)
                .FirstOrDefaultAsync(t => t.Id == trackId);

            return track?.Album?.CoverFilePath;
        }

        public async Task<string?> GetArtworkForAlbum(Guid albumId)
        {
            var album = await _context.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId);
            return album?.CoverFilePath;
        }

        public async Task<AlbumDto?> GetAlbum(Guid albumId)
        {
            var album = await _context.Albums
                .ProjectTo<AlbumDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync(a => a.Id == albumId);

            if (album == null)
            {
                return null;
            }

            await _artworkMappingHelper.MapArtworksToAlbums(album);
            await _favoritedMappingHelper.MapFavoritedToTracks(album.Tracks);

            album.Tracks = album.Tracks.OrderBy(t => t.DiscNumber).ThenBy(a => a.TrackNumber).ToList();
            return album;
        }

        public async Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId)
        {
            // Query DuckDB for similar tracks
            var similarTracks = await _embeddingService.GetSimilarTracksAsync(
                trackId,
                limit: 100,
                maxDistance: 0.2);

            if (!similarTracks.Any())
                return new List<SimpleTrackDto>();

            // Get track IDs (filtering by distance already done in EmbeddingService)
            var trackIds = similarTracks
                .DistinctBy(t => t.Distance)  // Identical distances = duplicates
                .Select(t => t.TrackId)
                .ToList();

            // Fetch full track info from SQLite
            var tracks = await _context.Tracks
                .Where(t => trackIds.Contains(t.Id))
                .ProjectTo<SimpleTrackDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            // Maintain order from similarity results
            var trackDict = tracks.ToDictionary(t => t.Id);
            var orderedTracks = trackIds
                .Where(id => trackDict.ContainsKey(id))
                .Select(id => trackDict[id])
                .ToList();

            await _favoritedMappingHelper.MapFavoritedToTracks(orderedTracks);

            return orderedTracks;
        }

        private record AlbumSimilarityScore
        {
            public int MatchCount { get; set; }
            public double TotalDistance { get; set; }
            public HashSet<Guid> MatchedTracks { get; init; } = new();
            public List<double> TrackDistances { get; init; } = new();
            public double AverageDistance => MatchCount > 0 ? TotalDistance / MatchCount : double.MaxValue;
            public double BestDistance => TrackDistances.Any() ? TrackDistances.Min() : 1.0;
            public double MedianDistance => TrackDistances.Any() ?
                TrackDistances.OrderBy(d => d).ElementAt(TrackDistances.Count / 2) : 1.0;

            // Average of best N tracks to prevent single/EP bias
            public double GetBestNAverage(int n = 3)
            {
                if (!TrackDistances.Any()) return 1.0;
                var bestN = TrackDistances.OrderBy(d => d).Take(Math.Min(n, TrackDistances.Count));
                return bestN.Average();
            }
        }

        public async Task<List<AlbumRecommendationDto>> GetRecommendationsForAlbum(Guid albumId)
        {
            // Get all tracks for the source album with their titles
            var albumTracksData = await _context.Tracks
                .Where(t => t.AlbumId == albumId)
                .Select(t => new { t.Id, t.Title })
                .ToListAsync();

            if (!albumTracksData.Any())
            {
                _logger.LogWarning("No tracks found for album {AlbumId}", albumId);
                return new List<AlbumRecommendationDto>();
            }

            var albumTracks = albumTracksData.Select(t => t.Id).ToList();
            // Create a set of source track titles to filter out same songs from singles/compilations
            var sourceTrackTitles = new HashSet<string>(
                albumTracksData.Select(t => t.Title.ToLowerInvariant()),
                StringComparer.InvariantCultureIgnoreCase);

            // Track intersection algorithm: Find albums that appear frequently in track recommendations
            var albumScores = new Dictionary<Guid, AlbumSimilarityScore>();

            // Limit tracks to check for performance (e.g., first 10 tracks for long albums)
            var tracksToCheck = albumTracks.Take(10).ToList();
            var tracksCheckedCount = tracksToCheck.Count;

            // Track the maximum match count to normalize scores
            int maxMatchCount = 0;
            double minDistance = double.MaxValue;

            foreach (var trackId in tracksToCheck)
            {
                // Get similar tracks for this track
                var similarTracks = await _embeddingService.GetSimilarTracksAsync(
                    trackId,
                    limit: 50,  // Fewer recommendations per track for performance
                    maxDistance: 0.25);  // Slightly more lenient for albums

                if (!similarTracks.Any())
                    continue;

                // Get album IDs and titles for similar tracks
                var similarTrackIds = similarTracks.Select(t => t.TrackId).ToList();
                var trackAlbumMap = await _context.Tracks
                    .Where(t => similarTrackIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.AlbumId, t.Title })
                    .ToListAsync();

                // Aggregate scores by album
                foreach (var similar in similarTracks)
                {
                    var trackAlbum = trackAlbumMap.FirstOrDefault(t => t.Id == similar.TrackId);
                    if (trackAlbum == null || trackAlbum.AlbumId == albumId)
                        continue; // Skip tracks without album or from the same album

                    // Skip if this track has the same title as one in the source album (e.g., single release)
                    if (sourceTrackTitles.Contains(trackAlbum.Title.ToLowerInvariant()))
                        continue;

                    if (!albumScores.TryGetValue(trackAlbum.AlbumId, out var score))
                    {
                        score = new AlbumSimilarityScore();
                        albumScores[trackAlbum.AlbumId] = score;
                    }

                    // Only count each track match once per album
                    if (!score.MatchedTracks.Contains(similar.TrackId))
                    {
                        score.MatchedTracks.Add(similar.TrackId);
                        score.MatchCount++;
                        score.TotalDistance += similar.Distance;
                        score.TrackDistances.Add(similar.Distance);

                        // Track max/min for normalization
                        maxMatchCount = Math.Max(maxMatchCount, score.MatchCount);
                        minDistance = Math.Min(minDistance, similar.Distance);
                    }
                }
            }

            if (!albumScores.Any())
            {
                _logger.LogInformation("No similar albums found for album {AlbumId}", albumId);
                return new List<AlbumRecommendationDto>();
            }

            // Sort albums primarily by average distance (ascending), then by match count (descending)
            // This prioritizes sonic similarity over simple match count
            var topAlbums = albumScores
                .OrderBy(kvp => kvp.Value.AverageDistance)
                .ThenByDescending(kvp => kvp.Value.MatchCount)
                .Take(10)  // Return top 10 albums (matches UI display)
                .ToList();

            // Fetch full album info
            var albumIds = topAlbums.Select(kvp => kvp.Key).ToList();
            var albums = await _context.Albums
                .Where(a => albumIds.Contains(a.Id))
                .ProjectTo<SimpleAlbumDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            // Get track counts for target albums to normalize scores
            var albumTrackCounts = await _context.Albums
                .Where(a => albumIds.Contains(a.Id))
                .Select(a => new { a.Id, TrackCount = a.Tracks.Count })
                .ToDictionaryAsync(a => a.Id, a => a.TrackCount);

            // Create recommendation DTOs with similarity scores
            var albumDict = albums.ToDictionary(a => a.Id);
            var recommendations = new List<AlbumRecommendationDto>();

            foreach (var albumScore in topAlbums)
            {
                if (!albumDict.TryGetValue(albumScore.Key, out var album))
                    continue;

                var score = albumScore.Value;

                // Get target album track count for normalization
                int targetAlbumTrackCount = albumTrackCounts.GetValueOrDefault(albumScore.Key, 10);

                // Simple, effective similarity calculation
                // Prioritize sonic similarity (distance) with basic match count consideration

                // Distance score: Use best 3 tracks average to prevent single/EP bias
                // Singles use their 1 track, albums use best 3, creating fair comparison
                double avgDist = score.GetBestNAverage(3);
                double distanceScore = 1.0 - Math.Min(avgDist * 2.5, 1.0); // Scale 0-0.4 distance to 1-0 score

                // Gentle penalty for very short releases
                // Just enough to prevent single-track dominance
                double albumSizePenalty = targetAlbumTrackCount switch
                {
                    1 => 0.75,     // Single track: 25% penalty
                    2 => 0.85,     // 2-track single: 15% penalty
                    3 => 0.92,     // 3-track EP: 8% penalty
                    _ => 1.0       // 4+ tracks: no penalty
                };

                // Match score: Balance between raw count and coverage
                // Use sqrt to reduce impact of album size without extreme bias either way
                double sizeAdjustedMatchScore = score.MatchCount / Math.Max(Math.Sqrt(targetAlbumTrackCount), 1.0);
                double normalizedMatchScore = Math.Min(1.0, sizeAdjustedMatchScore / 3.0); // Normalize to 0-1 range

                // Reduce match score weight since distance is more reliable
                // 92% distance, 8% match count - distance is what really matters
                double combinedScore = (distanceScore * 0.92) + (normalizedMatchScore * 0.08);

                // Apply album size penalty
                combinedScore *= albumSizePenalty;

                // Simple boost for very close matches to create some differentiation
                if (avgDist < 0.15)
                    combinedScore = Math.Min(1.0, combinedScore * 1.1);

                // Convert to percentage
                int similarityPercentage = (int)Math.Round(combinedScore * 100);

                recommendations.Add(new AlbumRecommendationDto
                {
                    Album = album,
                    SimilarityPercentage = Math.Min(100, Math.Max(0, similarityPercentage))
                });
            }

            // Sort by similarity score (highest first)
            recommendations = recommendations
                .OrderByDescending(r => r.SimilarityPercentage)
                .ToList();

            // Populate artwork for all recommended albums
            var albumDtos = recommendations.Select(r => r.Album).ToList();
            await _artworkMappingHelper.MapArtworksToAlbums(albumDtos);

            _logger.LogInformation("Found {Count} similar albums for album {AlbumId}", recommendations.Count, albumId);
            return recommendations;
        }

        public async Task<List<MusicLibraryDto>> GetMusicLibraries()
        {
            return await _context
                .MusicLibraries
                .ProjectTo<MusicLibraryDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        public async Task<MusicLibrary?> AddMusicLibrary(string path)
        {
            try
            {
                var contentDirectory = new DirectoryInfo(path);
                if (!contentDirectory.Exists)
                {
                    throw new ApplicationException("Content directory does not exist.");
                }

                var library = await _context.MusicLibraries.FirstOrDefaultAsync(m => m.LibraryPath == path)
                              ?? new MusicLibrary()
                              {
                                  LibraryPath = path,
                                  AudioFiles = new List<AudioFile>()
                              };

                _context.MusicLibraries.Add(library);
                await _context.SaveChangesAsync();

                // Queue initial scan after adding library
                var requestId = Guid.NewGuid();
                await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                    Library: library,
                    SpecificDirectory: null,
                    Incremental: false,
                    RequestId: requestId,
                    Trigger: ScanTrigger.LibraryAdded
                ));

                _logger.LogInformation("Library added and scan queued: {Path} (RequestId: {RequestId})", path, requestId);

                return library;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to add music library {Path}", path);
                return null;
            }
        }

        public async Task RemoveMusicLibrary(Guid libraryId)
        {
            var library = await _context.MusicLibraries.FindAsync(libraryId);
            if (library != null)
            {
                _context.MusicLibraries.Remove(library);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed music library {LibraryId} ({Path})", libraryId, library.LibraryPath);
            }
        }

        public async Task<MusicLibrary?> GetMusicLibrary(Guid libraryId)
        {
            return await _context.MusicLibraries.FindAsync(libraryId);
        }
    }
}
