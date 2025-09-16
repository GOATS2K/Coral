namespace Coral.Database.Models;

public enum ArtworkSize
{
    Small, Medium, Original
}

public class Artwork : BaseTable
{
    public int Width { get; set; }
    public int Height { get; set; }
    public ArtworkSize Size { get; set; }
    public string Path { get; set; } = null!;
    public Album Album { get; set; } = null!;
    public Guid AlbumId { get; set; }
    public string[] Colors { get; set; } = null!;
}