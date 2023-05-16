using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public class MusicLibraryDto
    {
        public int Id { get; set; }
        public string LibraryPath { get; set; } = null!;
        public DateTime LastScan { get; set; }
    }
}
