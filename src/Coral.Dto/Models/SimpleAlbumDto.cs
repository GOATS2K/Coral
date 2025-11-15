using Coral.Database.Models;

namespace Coral.Dto.Models
{
    public record SimpleAlbumDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public IEnumerable<SimpleArtistDto> Artists { get; set; } = null!;
        public AlbumType? Type { get; set; }
        public int ReleaseYear { get; set; }
        public ArtworkDto? Artworks { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Favorited { get; set; }
    }
}
