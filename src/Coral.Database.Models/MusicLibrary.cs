using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Database.Models
{
    public class MusicLibrary : BaseTable
    {
        public string LibraryPath { get; set; } = null!;
        public DateTime LastScan { get; set; }
        public List<AudioFile> AudioFiles { get; set; } = null!;
    }
}
