using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record ArtistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public List<SimpleAlbumDto> Releases { get; set; } = null!;
        public List<SimpleAlbumDto> FeaturedIn { get; set; } = null!;
        public List<SimpleAlbumDto> RemixerIn { get; set; } = null!;
    }
}
