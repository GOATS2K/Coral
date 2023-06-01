namespace Coral.Dto.Models
{
    public record ArtistDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> Releases { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> FeaturedIn { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> RemixerIn { get; set; } = null!;
        public IEnumerable<SimpleAlbumDto> InCompilation { get; set; } = null!;
    }
}
