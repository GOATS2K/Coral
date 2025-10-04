namespace Coral.Database.Models;

public class FavoriteAlbum :  BaseTable
{
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;
}