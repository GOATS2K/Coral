namespace Coral.Database.Models;

public class Album
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public List<Artist> Artists { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;

    public int? ReleaseYear { get; set; }
    public int? DiscTotal { get; set; }
    public int? TrackTotal { get; set; }

    public string? CoverFilePath { get; set; }

    public DateTime DateIndexed { get; set; }
    public DateTime DateModified { get; set; }
}