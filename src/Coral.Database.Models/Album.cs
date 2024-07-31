namespace Coral.Database.Models;

public enum AlbumType
{
    // 1-2 tracks
    Single,
    // 2-4 tracks
    EP,
    // 4 - 9 tracks
    MiniAlbum,
    // 10+ tracks
    Album,
    // Releases with 4 or more different artists
    Compilation
}

public class Album : BaseTable
{
    public string Name { get; set; } = null!;
    public List<ArtistWithRole> Artists { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;
    public AlbumType? Type { get; set; }

    public int? ReleaseYear { get; set; }
    public int? DiscTotal { get; set; }
    public int? TrackTotal { get; set; }
    public string? CoverFilePath { get; set; }
    public RecordLabel? Label { get; set; }
    public string? CatalogNumber { get; set; }
    public string? Upc { get; set; }

    public List<Artwork> Artworks { get; set; } = null!;
}