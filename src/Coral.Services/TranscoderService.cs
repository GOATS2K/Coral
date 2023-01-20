using CliWrap;
using Coral.Dto.EncodingModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Coral.Dto.Models;
using Coral.Encoders;
using Coral.Encoders.EncodingModels;

namespace Coral.Services
{
    public interface ITranscoderService
    {
        TranscodingJob GetJob(Guid id);
        void EndJob(Guid id);
        void CleanUpFiles(Guid id);
        public Task<TranscodingJob> CreateJob(OutputFormat format, Action<TranscodingJobRequest> requestConfiguration);
    }

    public class TranscoderService : ITranscoderService
    {
        private readonly List<TranscodingJob> _transcodingJobs = new List<TranscodingJob>();
        private readonly IEncoderFactory _encoderFactory;

        public TranscoderService(IEncoderFactory encoderFactory)
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

            if (!string.IsNullOrEmpty(job.OutputDirectory))
            {
                var parent = Directory.GetParent(job.OutputDirectory);
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

            if (!string.IsNullOrEmpty(job.OutputDirectory))
            {
                File.Delete(job.OutputDirectory);
            }
        }
        
        public async Task<TranscodingJob> CreateJob(OutputFormat format, Action<TranscodingJobRequest> requestConfiguration)
        {
            var requestData = new TranscodingJobRequest();
            requestConfiguration.Invoke(requestData);

            // check for existing job for the same file
            var existingJob = _transcodingJobs.FirstOrDefault(x => x.Request.SourceTrack.Id == requestData.SourceTrack.Id);
            if (existingJob != null)
            {
                await WaitForFile(Path.Combine(existingJob.OutputDirectory, existingJob?.FinalOutputFile));
                return existingJob;
            }

            TranscodingJob job = await CreateAndRunEncoderJob(format, requestData);

            return job;
        }

        private async Task<TranscodingJob> CreateAndRunEncoderJob(OutputFormat format, TranscodingJobRequest requestData)
        {
            var encoder = _encoderFactory.GetEncoder(format);
            if (encoder == null || !encoder.EnsureEncoderExists())
            {
                throw new ArgumentException("Unsupported format or missing encoder.");
            }

            // configure encoder
            var job = encoder.ConfigureTranscodingJob(requestData);
            _transcodingJobs.Add(job);

            // create command to run
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

            await WaitForFile(Path.Combine(job.OutputDirectory, job?.FinalOutputFile), () => CheckForTranscoderFailure(transcodingErrorStream, pipeErrorStream));
            return job;
        }

        private static async Task WaitForFile(string filePath, Action? action = null)
        {
            while (!File.Exists(filePath))
            {
                await Task.Delay(20);
                action?.Invoke();
            }
        }


        private static void CheckForTranscoderFailure(StringBuilder transcodingErrorStream, StringBuilder pipeErrorStream)
        {
            if (!string.IsNullOrEmpty(transcodingErrorStream.ToString())
                                || !string.IsNullOrEmpty(pipeErrorStream.ToString()))
            {
                throw new ApplicationException("Transcoder failed:\n" +
                                    $"Transcoder: {transcodingErrorStream}\n\n" +
                                    $"Pipe: {pipeErrorStream}\n");
            }
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
