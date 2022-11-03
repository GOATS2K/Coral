using CliWrap;
using CliWrap.Builders;
using CliWrap.EventStream;
using Coral.Configuration;
using Coral.Encoders.EncodingModels;

namespace Coral.Encoders.AAC;

// ➜ ffmpeg -i Wingz\ -\ Drawing\ Lines\ v4.wav -acodec aac_at -b:a 256k test.m4a
public class FfmpegBuilder : IArgumentBuilder
{
    private readonly List<string> _arguments = new List<string>();
    private readonly string _codec;
    private string _outputFile = "-";
    private string _inputFile;
    private bool _transcodeForHls;

    public FfmpegBuilder(string codec)
    {
        _codec = codec;
    }
    

    public string[] BuildArguments()
    {
        if (_transcodeForHls && _outputFile != "-")
        {
            throw new ArgumentException("You cannot generate HLS playlists and specify an output file at the same time");
        }

        var orderedArguments = new List<string>();
        orderedArguments.Add("-i");
        orderedArguments.Add(_inputFile);
        orderedArguments.Add("-acodec");
        orderedArguments.Add(_codec);
        orderedArguments.AddRange(_arguments);
        orderedArguments.Add("-hide_banner");
        orderedArguments.Add("-loglevel");
        orderedArguments.Add("error");
        return orderedArguments.ToArray();
    }

    public IArgumentBuilder GenerateHLSStream()
    {
        _transcodeForHls = true;
        _arguments.Add("-f");
        _arguments.Add("adts");
        SetDestinationFile("-");
        return this;
    }

    public IArgumentBuilder SetBitrate(int value)
    {
        _arguments.Add("-b:a");
        _arguments.Add($"{value}k");
        return this;
    }

    public IArgumentBuilder SetDestinationFile(string path)
    {
        _arguments.Add(path);
        return this;
    }

    public IArgumentBuilder SetSourceFile(string path)
    {
        _inputFile = path;
        return this;
    }
}


[EncoderFrontend(nameof(Ffmpeg), OutputFormat.AAC, Platform.MacOS)]
public class Ffmpeg : IEncoder
{

    public bool EnsureEncoderExists()
    {
        
        return CommonEncoderMethods.CheckEncoderExists("ffmpeg");
    }

    public IArgumentBuilder Configure()
    {
        return new FfmpegBuilder("aac_at");
    }
    
    public TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request)
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
            job.HlsPlaylistPath = Path.Combine(ApplicationConfiguration.HLSDirectory, job.Id.ToString(), "index.m3u8");
            job.PipeCommand = CommonEncoderMethods.GetHlsPipeCommand(job);
        }

        var arguments = configuration.BuildArguments();

        job.TranscodingCommand = Cli.Wrap("ffmpeg")
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .WithArguments(arguments);

        return job;
    }
}