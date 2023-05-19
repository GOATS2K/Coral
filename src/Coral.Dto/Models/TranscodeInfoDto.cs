using Coral.Dto.EncodingModels;

namespace Coral.Dto.Models
{
    public record TranscodeInfoDto
    {
        public required Guid JobId { get; set; }
        public required OutputFormat Format { get; set; } 
        public required int Bitrate { get; set; }
    }
}
