namespace Coral.Database.Models;

public class Track : BaseTable
{
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationInSeconds { get; set; }

    public string Title { get; set; } = null!;
    public List<ArtistWithRole> Artists { get; set; } = null!;
    public Album Album { get; set; } = null!;
    public Genre? Genre { get; set; }

    public string? Comment { get; set; }

    public AudioFile AudioFile { get; set; } = null!;
    public List<Keyword> Keywords { get; set; } = null!;

    public override string ToString()
    {
        var artistString = string.Join(", ", Artists.Where(a => a.Role == ArtistRole.Main).Select(a => a.Artist.Name));
        var releaseYear = Album.ReleaseYear != null ? $"({Album.ReleaseYear})" : "";
        return $"{artistString} - {Title} - {Album.Name} {releaseYear}";
    }
}
