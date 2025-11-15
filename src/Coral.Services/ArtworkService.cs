using Coral.BulkExtensions;
using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Dithering;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Coral.Services;

public interface IArtworkService
{
    Task ProcessArtwork(Album album, string artworkPath);
    Task ProcessArtworksBulk(Dictionary<Album, string> albumsWithArtwork);
    Task<List<Artwork>> ProcessArtworksParallel(Dictionary<Guid, string> albumArtworkPaths);
    Task<string?> ExtractEmbeddedArtwork(ATL.Track track);
    Task<string?> GetArtworkPath(Guid artworkId, ArtworkSize size);
    Task DeleteArtwork(Artwork artwork);
    Task<string[]> GetProminentColors(Guid artworkId);
    Task<string?> GetAlbumArtwork(Guid albumId, ArtworkSize size);
}

internal record PixelColor(Rgba32 Color, int Brightness);

public class ArtworkService : IArtworkService
{
    private readonly CoralDbContext _context;
    private readonly ILogger<ArtworkService> _logger;
    private readonly SemaphoreSlim _semaphore;

    public ArtworkService(CoralDbContext context, ILogger<ArtworkService> logger)
    {
        _context = context;
        _logger = logger;
        _semaphore = new SemaphoreSlim(Environment.ProcessorCount);
    }

    public async Task<string?> GetArtworkPath(Guid artworkId, ArtworkSize size)
    {
        var artwork = await _context.Artworks
            .Where(a => a.Id == artworkId)
            .FirstOrDefaultAsync();

        if (artwork == null) return null;

        return artwork.GetPath(size);
    }

    public async Task<string?> ExtractEmbeddedArtwork(ATL.Track track)
    {
        var outputDir = ApplicationConfiguration.ExtractedArtwork;
        // ensure directory is created
        Directory.CreateDirectory(outputDir);

        var guid = Guid.NewGuid();
        var outputFile = Path.Join(outputDir, $"{guid}.jpg");

        var artwork = track.EmbeddedPictures.FirstOrDefault();
        if (artwork == null)
        {
            return null;
        }

        _logger.LogDebug("Found embedded artwork in file {Track}", track.Path);
        var image = await Image.LoadAsync(new MemoryStream(artwork.PictureData));
        await image.SaveAsJpegAsync(outputFile);
        return outputFile;
    }

    public async Task ProcessArtwork(Album album, string artworkPath)
    {
        var guid = Guid.NewGuid();
        var outputDir = Path.Join(ApplicationConfiguration.Thumbnails, guid.ToString());
        Directory.CreateDirectory(outputDir);

        _logger.LogDebug("Processing artwork for album {AlbumTitle}", album.Name);

        // Extract prominent colors
        var prominentColors = await GetProminentColorsForImage(artworkPath);

        // Process all sizes and collect paths
        var sizes = new Dictionary<ArtworkSize, Size>()
        {
            {ArtworkSize.Small, new Size(100, 100)},
            {ArtworkSize.Medium, new Size(300, 300)}
        };

        var artworkPaths = new List<ArtworkPath>();

        // Add original artwork with its dimensions
        using (var originalImage = await Image.LoadAsync(artworkPath))
        {
            artworkPaths.Add(new ArtworkPath
            {
                Size = ArtworkSize.Original,
                Height = originalImage.Height,
                Width = originalImage.Width,
                Path = artworkPath
            });
        }

        // Process and add resized versions
        foreach (var (artworkSize, size) in sizes)
        {
            using var imageToResize = await Image.LoadAsync(artworkPath);
            var outputFile = Path.Join(outputDir, $"{artworkSize.ToString()}.jpg");
            imageToResize.Mutate(i => i.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
            await imageToResize.SaveAsJpegAsync(outputFile);

            artworkPaths.Add(new ArtworkPath
            {
                Size = artworkSize,
                Height = size.Height,
                Width = size.Width,
                Path = outputFile
            });
        }

        // Create single artwork with all paths in JSON
        album.Artwork = new Artwork
        {
            Paths = artworkPaths,
            Colors = prominentColors,
            AlbumId = album.Id
        };

        await _context.SaveChangesAsync();
    }

    public async Task ProcessArtworksBulk(Dictionary<Album, string> albumsWithArtwork)
    {
        if (!albumsWithArtwork.Any())
            return;

        _logger.LogInformation("Starting bulk artwork processing for {AlbumCount} albums", albumsWithArtwork.Count);

        var sizes = new Dictionary<ArtworkSize, Size>()
        {
            {ArtworkSize.Small, new Size(100, 100)},
            {ArtworkSize.Medium, new Size(300, 300)}
        };

        var artworksToInsert = new List<Artwork>();

        // Process each album's artwork and create Artwork entities
        foreach (var (album, artworkPath) in albumsWithArtwork)
        {
            try
            {
                var guid = Guid.NewGuid();
                var outputDir = Path.Join(ApplicationConfiguration.Thumbnails, guid.ToString());
                Directory.CreateDirectory(outputDir);

                _logger.LogDebug("Processing artwork for album {AlbumName}", album.Name);

                // Extract prominent colors once
                var prominentColors = await GetProminentColorsForImage(artworkPath);

                // Process all sizes
                var artworkPaths = new List<ArtworkPath>();

                // Add original artwork with its dimensions
                using (var originalImage = await Image.LoadAsync(artworkPath))
                {
                    artworkPaths.Add(new ArtworkPath
                    {
                        Size = ArtworkSize.Original,
                        Height = originalImage.Height,
                        Width = originalImage.Width,
                        Path = artworkPath
                    });
                }

                // Process and add resized versions
                foreach (var (artworkSize, size) in sizes)
                {
                    using var imageToResize = await Image.LoadAsync(artworkPath);
                    var outputFile = Path.Join(outputDir, $"{artworkSize.ToString()}.jpg");
                    imageToResize.Mutate(i => i.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
                    await imageToResize.SaveAsJpegAsync(outputFile);

                    artworkPaths.Add(new ArtworkPath
                    {
                        Size = artworkSize,
                        Height = size.Height,
                        Width = size.Width,
                        Path = outputFile
                    });
                }

                // Create single artwork entity with JSON paths
                artworksToInsert.Add(new Artwork
                {
                    Id = Guid.NewGuid(),
                    Paths = artworkPaths,
                    Colors = prominentColors,
                    AlbumId = album.Id,
                    CreatedAt = DateTime.UtcNow
                });

                _logger.LogDebug("Processed artwork for album: {AlbumName}", album.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to process artwork for album {AlbumName}: {Exception}",
                    album.Name, ex.ToString());
            }
        }

        // Bulk insert all artworks
        if (artworksToInsert.Any())
        {
            await _context.Artworks.AddRangeAsync(artworksToInsert);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Bulk artwork processing completed: {Artworks} artworks inserted for {Albums} albums",
                artworksToInsert.Count, albumsWithArtwork.Count);
        }
    }

    public async Task<List<Artwork>> ProcessArtworksParallel(Dictionary<Guid, string> albumArtworkPaths)
    {
        if (!albumArtworkPaths.Any())
            return new List<Artwork>();

        _logger.LogInformation("Starting parallel artwork processing for {AlbumCount} albums", albumArtworkPaths.Count);

        var sizes = new Dictionary<ArtworkSize, Size>()
        {
            {ArtworkSize.Small, new Size(100, 100)},
            {ArtworkSize.Medium, new Size(300, 300)}
        };

        // Use ConcurrentBag for thread-safe collection
        var artworkResults = new System.Collections.Concurrent.ConcurrentBag<Artwork>();

        // Process albums in parallel with bounded parallelism
        var processingTasks = albumArtworkPaths.Select(async kvp =>
        {
            await _semaphore.WaitAsync();
            try
            {
                var albumId = kvp.Key;
                var artworkPath = kvp.Value;

                var guid = Guid.NewGuid();
                var outputDir = Path.Join(ApplicationConfiguration.Thumbnails, guid.ToString());
                Directory.CreateDirectory(outputDir);

                _logger.LogDebug("Processing artwork for album {AlbumId}", albumId);

                // Extract prominent colors once
                var prominentColors = await GetProminentColorsForImage(artworkPath);

                // Process all sizes
                var artworkPaths = new List<ArtworkPath>();

                // Add original artwork with its dimensions
                using (var originalImage = await Image.LoadAsync(artworkPath))
                {
                    artworkPaths.Add(new ArtworkPath
                    {
                        Size = ArtworkSize.Original,
                        Height = originalImage.Height,
                        Width = originalImage.Width,
                        Path = artworkPath
                    });
                }

                // Process and add resized versions
                foreach (var (artworkSize, size) in sizes)
                {
                    using var imageToResize = await Image.LoadAsync(artworkPath);
                    var outputFile = Path.Join(outputDir, $"{artworkSize.ToString()}.jpg");
                    imageToResize.Mutate(i => i.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
                    await imageToResize.SaveAsJpegAsync(outputFile);

                    artworkPaths.Add(new ArtworkPath
                    {
                        Size = artworkSize,
                        Height = size.Height,
                        Width = size.Width,
                        Path = outputFile
                    });
                }

                // Create single artwork entity
                var artwork = new Artwork
                {
                    Id = Guid.NewGuid(),
                    Paths = artworkPaths,
                    Colors = prominentColors,
                    AlbumId = albumId,
                    CreatedAt = DateTime.UtcNow
                };

                artworkResults.Add(artwork);
                _logger.LogDebug("Processed artwork for album {AlbumId}", albumId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process artwork for album {AlbumId}", kvp.Key);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        // Wait for all parallel processing to complete
        await Task.WhenAll(processingTasks);

        var allArtworks = artworkResults.ToList();

        _logger.LogInformation(
            "Parallel artwork processing completed: {Artworks} artworks created for {Albums} albums",
            allArtworks.Count, albumArtworkPaths.Count);

        return allArtworks;
    }

    // https://www.nbdtech.com/Blog/archive/2008/04/27/Calculating-the-Perceived-Brightness-of-a-Color.aspx
    private static int GetBrightness(Rgba32 c)
    {
        return (int)Math.Sqrt(
            c.R * c.R * .241 +
            c.G * c.G * .691 +
            c.B * c.B * .068);
    }

    public async Task<string[]> GetProminentColors(Guid artworkId)
    {
        var artwork = await _context.Artworks
            .FirstOrDefaultAsync(t => t.Id == artworkId);

        if (artwork == null)
            return [];

        return artwork.Colors;
    }

    public async Task<string?> GetAlbumArtwork(Guid albumId, ArtworkSize size)
    {
        var artwork = await _context.Artworks.FirstOrDefaultAsync(t => t.AlbumId == albumId);

        if (artwork == null) return null;

        return artwork.GetPath(size);
    }

    private async Task<string[]> GetProminentColorsForImage(string filePath)
    {
        using var image = await Image.LoadAsync<Rgba32>(filePath);

        image.Mutate(x => x
            // Scale the image down preserving the aspect ratio. This will speed up quantization.
            // We use nearest neighbor as it will be the fastest approach.
            .Resize(150, 0, new NearestNeighborResampler())
            .Quantize(new OctreeQuantizer(new QuantizerOptions()
            {
                ColorMatchingMode = ColorMatchingMode.Exact,
                MaxColors = 6
            })));

        var pixels = new List<PixelColor>();
        for (var i = 0; i < image.Width; i++)
        {
            for (var j = 0; j < image.Height; j++)
            {
                try
                {
                    var color = image[i, j];
                    pixels.Add(new PixelColor(color, GetBrightness(color)));
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    _logger.LogError(ex, "Failed to get colors for artwork: {FilePath}", filePath);
                    _logger.LogInformation("I: {IValue}, J: {JValue} - Dimensions: {ImageDimensions}", i, j, $"{image.Width}x{image.Height}");
                }
            }
        }

        var colors = pixels
            .DistinctBy(c => c.Color)
            .OrderByDescending(c => c.Brightness)
            .Select(c => $"#{c.Color.ToHex().Substring(0, 6)}")
            .Take(3)
            .ToArray();

        _logger.LogDebug("Successfully got artwork colors for {FilePath}", filePath);

        return colors;
    }

    public async Task DeleteArtwork(Artwork artwork)
    {
        // Delete all size variants from the Paths list
        var paths = artwork.Paths
            .Select(p => p.Path)
            .Where(p => !string.IsNullOrEmpty(p) && p.StartsWith(ApplicationConfiguration.AppData));

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted local artwork file: {ArtworkPath}", path);
            }
        }

        // Clean up empty directories
        var firstPath = artwork.Paths.FirstOrDefault()?.Path;
        if (!string.IsNullOrEmpty(firstPath))
        {
            var parent = Directory.GetParent(firstPath)?.FullName;
            if (parent != null && Directory.Exists(parent) && !Directory.GetFiles(parent).Any())
            {
                Directory.Delete(parent);
            }
        }

        _context.Remove(artwork);
        await _context.SaveChangesAsync();
    }
}
