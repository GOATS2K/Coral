using Coral.Dto.Models;

namespace Coral.Services.Models
{
    public record SearchResult
    {
        public List<SimpleArtistDto> Artists { get; init; } = null!;
        public List<SimpleAlbumDto> Albums { get; init; } = null!;
        public List<TrackDto> Tracks { get; init; } = null!;
    }
}
