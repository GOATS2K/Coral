namespace Coral.Dto.Models
{
    public record StreamDto
    {
        public required string Link { get; set; }
        public TranscodeInfoDto? TranscodeInfo { get; set; }
        public string? ArtworkUrl { get; set; }
    }
}
