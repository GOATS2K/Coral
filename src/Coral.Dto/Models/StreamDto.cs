using Coral.Encoders.EncodingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Dto.Models
{
    public record StreamDto
    {
        public required string Link { get; set; }
        public required int RequestedBitrate { get; set; }
        public required OutputFormat RequestedFormat { get; set; }
    }
}
