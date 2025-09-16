namespace Coral.Database.Models
{
    public enum ArtistRole
    {
        Main, Guest, Remixer
    }

    public class ArtistWithRole : BaseTable
    {
        public ArtistRole Role { get; set; }
        public Guid ArtistId { get; set; }
        public Artist Artist { get; set; } = null!;
        public List<Track> Tracks { get; set; } = null!;
        public List<Album> Albums { get; set; } = null!;
    }
}
