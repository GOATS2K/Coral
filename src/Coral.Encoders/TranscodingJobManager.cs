using Coral.Encoders.EncodingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Coral.Encoders
{
    public interface ITranscodingJobManager
    {
        TranscodingJob GetTranscodingJob(Guid id);
        void EndTranscodingJob(Guid id);
        void CleanUpTranscodeFiles(Guid id);
    }

    public class TranscodingJobManager : ITranscodingJobManager
    {
        private readonly List<TranscodingJob> _transcodingJobs = new List<TranscodingJob>();
        private readonly IEncoderFactory _encoderFactory;

        public TranscodingJobManager(IEncoderFactory encoderFactory)
        {
            _encoderFactory = encoderFactory;
        }

        public void CleanUpTranscodeFiles(Guid id)
        {
            var job = _transcodingJobs.FirstOrDefault(x => x.Id == id);
            if (job == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(job.HlsPlaylistPath))
            {
                Directory.Delete(job.HlsPlaylistPath);
            }

            if (!string.IsNullOrEmpty(job.OutputPath))
            {
                File.Delete(job.OutputPath);
            }
        }

        public void EndTranscodingJob(Guid id)
        {
            throw new NotImplementedException();
        }

        public TranscodingJob GetTranscodingJob(Guid id)
        {
            return _transcodingJobs.First(x => x.Id == id);
        }
    }
}
