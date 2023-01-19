using CliWrap;
using Coral.Dto.EncodingModels;
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
        public string OutputDirectory { get; set; } = null!;
        public string FinalOutputFile { get; set; } = null!;
        public Command? TranscodingCommand { get; set; }
        public Command? PipeCommand { get; set; }
        public bool EncoderWritesToStandardError;
    }
}
