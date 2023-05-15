using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Database.Models
{
    public class AudioFile : BaseTable
    {
        public string FilePath { get; set; } = null!;
        public decimal FileSizeInBytes { get; set; }
        public AudioMetadata AudioMetadata { get; set; } = null!;
        public MusicLibrary Library { get; set; } = null!;
    }
}
