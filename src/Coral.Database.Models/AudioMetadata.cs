namespace Coral.Database.Models
{
    public class AudioMetadata : BaseTable
    {
        public int Bitrate { get; set; }
        public int? BitDepth { get; set; }
        public double SampleRate { get; set; }
        public int? Channels { get; set; }
        public string Codec { get; set; } = null!;
    }
}
