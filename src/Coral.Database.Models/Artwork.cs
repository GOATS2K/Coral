namespace Coral.Database.Models;

public enum ArtworkSize
{
    Small, Medium, Original
}

// Individual artwork path entry
public class ArtworkPath
{
    public ArtworkSize Size { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
    public string Path { get; set; } = null!;
}

public class Artwork : BaseTable
{
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;

    // JSON column - list of artwork paths with dimensions
    public List<ArtworkPath> Paths { get; set; } = new();

    public string[] Colors { get; set; } = null!;

    // Helper method to get path by size
    public string? GetPath(ArtworkSize size) => Paths.FirstOrDefault(p => p.Size == size)?.Path;
}