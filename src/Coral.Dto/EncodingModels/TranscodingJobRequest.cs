using Coral.Database.Models;

namespace Coral.Dto.EncodingModels
{
    public enum TranscodeRequestType
    {
        SingleFile, HLS
    }

    public class TranscodingJobRequest
    {
        public Track SourceTrack { get; set; } = null!;
        public TranscodeRequestType RequestType { get; set; }
        public int Bitrate { get; set; }
    }
}
