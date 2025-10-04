namespace Coral.Database.Models;

public class FavoriteTrack : BaseTable
{
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = null!;
}