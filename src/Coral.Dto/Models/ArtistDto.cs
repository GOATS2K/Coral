namespace Coral.Dto.Models
{
    public record ArtistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public bool Favorited { get; set; }
        public IEnumerable<SimpleAlbumDto> Releases { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> FeaturedIn { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> RemixerIn { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> InCompilation { get; set; } = null!;
    }
}
