namespace Coral.Database.Models;

public class Playlist : BaseTable
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<PlaylistTrack> Tracks { get; set; } = null!;
}