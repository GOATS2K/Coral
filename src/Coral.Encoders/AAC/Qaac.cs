using Coral.Dto.EncodingModels;

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


[EncoderFrontend(nameof(Qaac), OutputFormat.AAC, Platform.Windows)]
public class Qaac : IEncoder
{
    public string ExecutableName => "qaac";

    public bool WritesOutputToStdErr => true;

    public bool EnsureEncoderExists()
    {
        return CommonEncoderMethods.CheckEncoderExists(ExecutableName);
    }

    public IArgumentBuilder Configure()
    {
        return new QaacBuilder();
    }
}