using Coral.Dto.EncodingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record TranscodeInfoDto
    {
        public required Guid JobId { get; set; }
        public required OutputFormat Format { get; set; } 
        public required int Bitrate { get; set; }
    }
}
