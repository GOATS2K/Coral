using Coral.Configuration;
using Coral.Database;
using Coral.Database.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Coral.Services;

public interface IArtworkService
{
    public Task ProcessArtwork(Album album, string artworkPath);
    public Task<string?> ExtractEmbeddedArtwork(ATL.Track track);
}

public class ArtworkService : IArtworkService
{
    private readonly CoralDbContext _context;
    private readonly ILogger<ArtworkService> _logger;
    
    public ArtworkService(CoralDbContext context, ILogger<ArtworkService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<string?> ExtractEmbeddedArtwork(ATL.Track track)
    {
        var outputDir = ApplicationConfiguration.ExtractedArtwork;
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
                Size = artworkSize
            });
        }
        
        // after processing thumbnails, add original cover as well
        using var originalImage = await Image.LoadAsync(artworkPath);
        album.Artworks.Add(new Artwork
        {
            Path = artworkPath,
            Size = ArtworkSize.Original,
            Height = originalImage.Height,
            Width = originalImage.Width
        });
        
        await _context.SaveChangesAsync();
    }
}