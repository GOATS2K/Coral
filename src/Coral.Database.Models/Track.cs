namespace Coral.Database.Models;

public class Track
{
    public int Id { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public int DurationInSeconds { get; set; }

    public string Title { get; set; } = null!;
    public Artist Artist { get; set; } = null!;
    public Album? Album { get; set; }
    public Genre? Genre { get; set; }
    
    public string? Comment { get; set; }
    
    public string FilePath { get; set; } = null!;
    
    public DateTime DateIndexed { get; set; }
    public DateTime DateModified { get; set; }
}
