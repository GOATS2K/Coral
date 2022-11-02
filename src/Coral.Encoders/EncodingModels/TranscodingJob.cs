using Coral.Database.Models;
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
        public Track SourceTrack { get; set; } = null!;
        public string? OutputPath { get; set; }
        public string? HlsPlaylistPath { get; set; }
        public OutputFormat RequestedFormat { get; set; }
        public bool TranscodingProcessHasExited { get; set; }
    }
}
