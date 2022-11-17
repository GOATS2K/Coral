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
                var parent = Directory.GetParent(job.HlsPlaylistPath);
                if (parent == null)
                {
                    return;
                }

                foreach (var file in parent.EnumerateFiles())
                {
                    file.Delete();
                }
                parent.Delete();
                
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
                throw new ArgumentException("Unsupported format or missing encoder.");
            }

            // check for existing job for the same file
            var existingJob = _transcodingJobs.FirstOrDefault(x => x.Request.SourceTrack.Id == requestData.SourceTrack.Id);
            if (existingJob != null)
            {
                return existingJob;
            }

            // configure encoder
            var job = encoder.ConfigureTranscodingJob(requestData);
            _transcodingJobs.Add(job);

            // run job
            Command? jobCommand;
            var transcodingErrorStream = new StringBuilder();
            var pipeErrorStream = new StringBuilder();
            
            // only save error logs if the encoder writes regular logs to stdout (looking at you qaac);
            if (!encoder.WritesOutputToStdErr)
            {
                job.TranscodingCommand = job.TranscodingCommand!.WithStandardErrorPipe(PipeTarget.ToStringBuilder(transcodingErrorStream));
            }
            

            if (job.PipeCommand != null)
            {
                jobCommand = (job.TranscodingCommand! | job.PipeCommand.WithStandardErrorPipe(PipeTarget.ToStringBuilder(pipeErrorStream)));
            }
            else
            {
                jobCommand = job.TranscodingCommand!;
            }
            
            #pragma warning disable CS4014 // I want this to run in the background.
            jobCommand.ExecuteAsync();
            #pragma warning restore CS4014
            
            while (!File.Exists(job.HlsPlaylistPath))
            {
                await Task.Delay(200);
    
                if (!string.IsNullOrEmpty(transcodingErrorStream.ToString()) 
                    || !string.IsNullOrEmpty(pipeErrorStream.ToString()))
                {
                    throw new ApplicationException("Transcoder failed:\n" +
                                        $"Transcoder: {transcodingErrorStream}\n\n" +
                                        $"Pipe: {pipeErrorStream}\n");
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
