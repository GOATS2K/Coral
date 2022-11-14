using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record SimpleAlbumDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int ReleaseYear { get; set; }
        public bool CoverPresent { get; set; }
    }
}
