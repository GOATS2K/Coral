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
    Task<string?> ExtractEmbeddedArtwork(ATL.Track track);
    Task<string?> GetArtworkPath(int artworkId);
    Task DeleteArtwork(Artwork artwork);
    Task<string[]> GetProminentColors(int artworkId);
}

internal record PixelColor(Rgba32 Color, int Brightness);

public class ArtworkService : IArtworkService
{
    private readonly CoralDbContext _context;
    private readonly ILogger<ArtworkService> _logger;

    public ArtworkService(CoralDbContext context, ILogger<ArtworkService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string?> GetPathForOriginalAlbumArtwork(int albumId)
    {
        return await _context.Artworks
            .Where(a => a.Album.Id == albumId && a.Size == ArtworkSize.Original)
            .Select(a => a.Path)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetArtworkPath(int artworkId)
    {
        return await _context.Artworks
            .Where(a => a.Id == artworkId)
            .Select(a => a.Path)
            .FirstOrDefaultAsync();
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

        _logger.LogInformation("Found embedded artwork in file {Track}", track.Path);
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
        
        // add original cover
        using var originalImage = await Image.LoadAsync(artworkPath);
        var prominentColors = await GetProminentColorsForImage(artworkPath);
        album.Artworks.Add(new Artwork
        {
            Path = artworkPath,
            Size = ArtworkSize.Original,
            Height = originalImage.Height,
            Width = originalImage.Width,
            Colors = prominentColors,
        });

        var sizes = new Dictionary<ArtworkSize, Size>()
        {
            {ArtworkSize.Small, new Size(100, 100)},
            {ArtworkSize.Medium, new Size(300, 300)}
        };

        foreach (var (artworkSize, size) in sizes)
        {
            // because image mutations affect the loaded image,
            // we'll reload and dispose for each mutation
            using var imageToResize = await Image.LoadAsync(artworkPath);
            var outputFile = Path.Join(outputDir, $"{artworkSize.ToString()}.jpg");
            imageToResize.Mutate(i => i.Resize(size.Width, size.Height, KnownResamplers.Lanczos3));
            await imageToResize.SaveAsJpegAsync(outputFile);
            album.Artworks.Add(new Artwork()
            {
                Path = outputFile,
                Height = size.Height,
                Width = size.Width,
                Size = artworkSize,
                Colors = prominentColors,
            });
        }

        await _context.SaveChangesAsync();
    }
    
    // https://www.nbdtech.com/Blog/archive/2008/04/27/Calculating-the-Perceived-Brightness-of-a-Color.aspx
    private static int GetBrightness(Rgba32 c)
    {
        return (int)Math.Sqrt(
            c.R * c.R * .241 + 
            c.G * c.G * .691 + 
            c.B * c.B * .068);
    }

    public async Task<string[]> GetProminentColors(int artworkId)
    {
        var artwork = await _context.Artworks
            .FirstOrDefaultAsync(t => t.Id == artworkId);

        if (artwork == null)
            return [];

        return await GetProminentColorsForImage(artwork.Path);
    }

    private static async Task<string[]> GetProminentColorsForImage(string filePath)
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
        for (var i = 0; i < image.Height; i++)
        {
            for (var j = 0; j < image.Width; j++)
            {
                var color = image[i, j];
                pixels.Add(new PixelColor(color, GetBrightness(color)));
            }
        }

        var colors = pixels
            .DistinctBy(c => c.Color)
            .OrderByDescending(c => c.Brightness)
            .Select(c => $"#{c.Color.ToHex().Substring(0, 6)}")
            .Take(3)
            .ToArray();
        
        return colors;
    }

    public async Task DeleteArtwork(Artwork artwork)
    {
        // remove artwork file if its in the AppData folder
        if (artwork.Path.StartsWith(ApplicationConfiguration.AppData))
        {
            File.Delete(artwork.Path);
            _logger.LogInformation("Deleted local artwork file: {ArtworkPath}", artwork.Path);
            var parent = Directory.GetParent(artwork.Path)?.FullName;

            if (parent != null && !Directory.GetFiles(parent).Any())
            {
                Directory.Delete(parent);
            }
        }

        _context.Remove(artwork);
        await _context.SaveChangesAsync();
    }
}