using Coral.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public class ArtistOnTrackDto
    {
        public SimpleArtistDto Artist { get; set; } = null!;
        public ArtistRole Role { get; set; }
    }
}
