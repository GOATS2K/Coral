using Coral.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Encoders.EncodingModels
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
