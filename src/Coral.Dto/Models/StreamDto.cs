using Coral.Dto.EncodingModels;
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
        public TranscodeInfoDto? TranscodeInfo { get; set; }
        public string MimeType { get; set; } = null!;

        public string? ArtworkUrl { get; set; }
    }
}
