namespace Coral.Database.Models;

public class Artist : BaseTable
{
    public string Name { get; set; } = null!;
    public List<ArtistWithRole> Roles { get; set; } = null!;
    public FavoriteArtist? Favorite { get; set; }
}