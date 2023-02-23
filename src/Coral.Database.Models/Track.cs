namespace Coral.Database.Models;

public class Track : BaseTable
{
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationInSeconds { get; set; }

    public string Title { get; set; } = null!;
    public List<ArtistOnTrack> Artists { get; set; } = null!;
    public Album Album { get; set; } = null!;
    public Genre? Genre { get; set; }

    public string? Comment { get; set; }

    public string FilePath { get; set; } = null!;
    public List<Keyword> Keywords { get; set; } = null!;

    public override string ToString()
    {
        var artistString = string.Join(", ", Artists.Select(a => a.Artist.Name));
        var releaseYear = Album.ReleaseYear != null ? $"({Album.ReleaseYear})" : "";
        return $"{artistString} - {Title} - {Album.Name} {releaseYear}";
    }
}
