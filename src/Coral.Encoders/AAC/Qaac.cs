using CliWrap;
using CliWrap.Builders;
using CliWrap.EventStream;
using Coral.Configuration;
using Coral.Encoders.EncodingModels;

namespace Coral.Encoders.AAC;

public class QaacBuilder : IArgumentBuilder
{
    private readonly List<string> _arguments = new List<string>();
    private string _outputFile = "-";
    private string _inputFile = String.Empty;
    private bool _transcodeForHls = false;

    public string[] BuildArguments()
    {
        if (_transcodeForHls && _outputFile != "-")
        {
            throw new ArgumentException("You cannot generate HLS playlists and specify an output file at the same time");
        }

        var orderedArguments = new List<string>();
        orderedArguments.Add(_inputFile);
        if (_transcodeForHls) orderedArguments.Add("--adts");
        orderedArguments.AddRange(_arguments);
        return orderedArguments.ToArray();
    }

    public IArgumentBuilder GenerateHLSStream()
    {
        _transcodeForHls = true;
        SetDestinationFile("-");
        return this;
    }

    public IArgumentBuilder SetBitrate(int value)
    {
        _arguments.Add("-c");
        _arguments.Add($"{value}");
        return this;
    }

    public IArgumentBuilder SetDestinationFile(string path)
    {
        _arguments.Add("-o");
        _arguments.Add(path);
        return this;
    }

    public IArgumentBuilder SetSourceFile(string path)
    {
        _inputFile = path;
        return this;
    }
}


[EncoderFrontend("Qaac", OutputFormat.AAC, Platform.Windows)]
public class Qaac : IEncoder
{

    public bool EnsureEncoderExists()
    {
        var mp4BoxStatus = CommonEncoderMethods.CheckEncoderExists("MP4Box");
        var qaacStatus = CommonEncoderMethods.CheckEncoderExists("qaac");
        return mp4BoxStatus && qaacStatus;
    }

    public IArgumentBuilder Configure()
    {
        return new QaacBuilder();
    }

    // qaac "01 - Alix Perez - Wondering at Loss.flac" --adts -c 256 -o- | MP4Box -dash 5000 -dynamic -segment-name chunk-$Number$ -profile onDemand -out hls/test.m3u8 stdin:#Bitrate=256000:#Duration=266
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
            var jobDir = Path.Combine(ApplicationConfiguration.HLSDirectory, job.Id.ToString());
            Directory.CreateDirectory(jobDir);
            job.HlsPlaylistPath = Path.Combine(ApplicationConfiguration.HLSDirectory, job.Id.ToString(), "index.m3u8");
            job.PipeCommand = Cli.Wrap("ffmpeg")
                .WithArguments(new string[]
                {
                    "-i",
                    "-",
                    "-f",
                    "hls",
                    "-hls_time",
                    "5",
                    "-hls_playlist_type",
                    "event",
                    "-hls_segment_filename",
                    $"{Path.Combine(jobDir, "chunk-%02d.ts")}",
                    $"{job.HlsPlaylistPath}"
                });
        }

        var arguments = configuration.BuildArguments();

        job.TranscodingCommand = Cli.Wrap("qaac")
            .WithArguments(arguments);

        return job;
    }
}