using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record SimpleArtistDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }
}
