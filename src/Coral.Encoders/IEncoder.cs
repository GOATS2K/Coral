using CliWrap;
using Coral.Configuration;
using Coral.Encoders.EncodingModels;

namespace Coral.Encoders;

public interface IEncoder
{
    public string ExecutableName { get; }
    public bool WritesOutputToStdErr { get; }

    bool EnsureEncoderExists();
    IArgumentBuilder Configure();
    virtual TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request)
    {
        var job = new TranscodingJob()
        {
            Request = request,
        };

        var configuration = Configure()
            .SetSourceFile(request.SourceTrack.FilePath)
            .SetBitrate(request.Bitrate);

        if (request.RequestType == TranscodeRequestType.HLS)
        {
            configuration.GenerateHLSStream();
            job.OutputDirectory = Path.Combine(ApplicationConfiguration.HLSDirectory,
                job.Id.ToString());
            job.FinalOutputFile = "master.m3u8";
            job.PipeCommand = CommonEncoderMethods.GetHlsPipeCommand(job);
        }

        var arguments = configuration.BuildArguments();

        job.TranscodingCommand = Cli.Wrap(ExecutableName)
            .WithArguments(arguments);

        return job;
    }
}