using CliWrap;
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
        TranscodingJob GetJob(Guid id);
        void EndJob(Guid id);
        void CleanUpFiles(Guid id);
        public Task<TranscodingJob> CreateJob(OutputFormat format, Action<TranscodingJobRequest> requestConfiguration);
    }

    public class TranscodingJobManager : ITranscodingJobManager
    {
        private readonly List<TranscodingJob> _transcodingJobs = new List<TranscodingJob>();
        private readonly IEncoderFactory _encoderFactory;

        public TranscodingJobManager(IEncoderFactory encoderFactory)
        {
            _encoderFactory = encoderFactory;
        }

        public void CleanUpFiles(Guid id)
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

        public async Task<TranscodingJob> CreateJob(OutputFormat format, Action<TranscodingJobRequest> requestConfiguration)
        {
            var requestData = new TranscodingJobRequest();
            requestConfiguration.Invoke(requestData);

            var encoder = _encoderFactory.GetEncoder(format);
            if (encoder == null || !encoder.EnsureEncoderExists())
            {
                throw new ArgumentException("Unsupported format.");
            }

            // check for existing job for the same file
            var existingJob = _transcodingJobs.Where(x => x.Request.SourceTrack.Id == requestData.SourceTrack.Id).FirstOrDefault();
            if (existingJob != null)
            {
                return existingJob;
            }

            // configure encoder
            var job = encoder.ConfigureTranscodingJob(requestData);
            _transcodingJobs.Add(job);

            // run job
            Command? jobCommand = null;
            if (job.PipeCommand != null)
            {
                jobCommand = (job.TranscodingCommand! | job.PipeCommand);
            }
            else
            {
                jobCommand = job.TranscodingCommand!;
            }

            jobCommand.ExecuteAsync();

            int msWaited = 0;
            while (!File.Exists(job.HlsPlaylistPath))
            {
                await Task.Delay(100);
                msWaited += 100;

                if (msWaited == 20000)
                {
                    throw new ApplicationException("Transcoder timed out.");
                }
            }

            return job;
        }

        public void EndJob(Guid id)
        {
            throw new NotImplementedException();
        }

        public TranscodingJob GetJob(Guid id)
        {
            return _transcodingJobs.First(x => x.Id == id);
        }
    }
}
