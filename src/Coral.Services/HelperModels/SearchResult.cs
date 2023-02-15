using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record SearchResult
    {
        public List<SimpleArtistDto> Artists { get; init; } = null!;
        public List<SimpleAlbumDto> Albums { get; init; } = null!;
        public List<TrackDto> Tracks { get; init; } = null!;
    }
}
