namespace Coral.Database.Models;

public class Genre : BaseTable
{
    public string Name { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;
}