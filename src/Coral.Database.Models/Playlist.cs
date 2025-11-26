namespace Coral.Database.Models;

public enum PlaylistType
{
    Normal = 0,
    LikedSongs = 1
}

public class Playlist : BaseTable
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public PlaylistType Type { get; set; } = PlaylistType.Normal;
    public List<PlaylistTrack> Tracks { get; set; } = null!;
}