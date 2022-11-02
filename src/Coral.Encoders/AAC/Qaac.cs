using Coral.Encoders.EncodingModels;

namespace Coral.Encoders.AAC;


// qaac "01 - Alix Perez - Wondering at Loss.flac" --adts -c 256 -o- | MP4Box -dash-live 5000 -segment-name chunk-$Number$ -profile onDemand -out hls/test.m3u8 stdin:#Bitrate=256000:#Duration=266
public class QaacBuilder : IArgumentBuilder
{
    private readonly List<string> _arguments = new List<string>();
    private string _outputFile = "-";
    private string _inputFile = String.Empty;
    private bool _transcodeForHls = false;

    public string[] BuildArguments()
    {
        var orderedArguments = new List<string>();
        orderedArguments.Add(_inputFile);
        if (_transcodeForHls) orderedArguments.Add("--adts");
        orderedArguments.AddRange(_arguments);
        return orderedArguments.ToArray();
    }

    public IArgumentBuilder GenerateHLSStream()
    {
        _transcodeForHls = true;
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
}