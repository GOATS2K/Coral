namespace Coral.Database.Models;

public class FavoriteArtist : BaseTable
{
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;
}