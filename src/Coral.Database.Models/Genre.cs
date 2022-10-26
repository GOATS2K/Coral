namespace Coral.Database.Models;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;
    public DateTime DateIndexed { get; set; }
}