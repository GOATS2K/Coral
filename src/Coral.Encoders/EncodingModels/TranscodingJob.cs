using CliWrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coral.Encoders.EncodingModels
{
    public class TranscodingJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TranscodingJobRequest Request { get; set; } = null!;
        public string? OutputPath { get; set; }
        public string? HlsPlaylistPath { get; set; }
        public Command? TranscodingCommand { get; set; }
        public Command? PipeCommand { get; set; }
        public bool EncoderWritesToStandardError;
    }
}
