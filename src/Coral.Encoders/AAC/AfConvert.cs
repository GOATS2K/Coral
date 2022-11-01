using System.Diagnostics;

namespace Coral.Encoders.AAC;

public class AfConvertBuilder : IArgumentBuilder
{
    private string _inputFile = string.Empty;
    private string _outputFile = string.Empty;
    private readonly List<string> _arguments;
    public AfConvertBuilder()
    {
        _arguments = new List<string>();
    }
    public IArgumentBuilder SetBitrate(int value)
    {
        var bitrate = value * 1000;
        _arguments.Add("-b");
        _arguments.Add($"{bitrate}");

        return this;
    }

    public IArgumentBuilder SetSourceFile(string path)
    {
        _inputFile = path;
        return this;
    }

    public IArgumentBuilder SetDestinationFile(string path)
    {
        _outputFile = path;
        return this;
    }

    public Stream Transcode()
    {
        if (string.IsNullOrEmpty(_outputFile))
        {
            _outputFile = Path.GetTempFileName();
        }
        // create output file for reading
        File.Create(_outputFile);

        var startInfo = CreateStartInfo();

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new ApplicationException("Transcoder failed to execute.");
        }

        // var stdOut = process.StandardOutput.ReadToEnd();
        // var stdErr = process.StandardError.ReadToEnd();
        // if (!string.IsNullOrWhiteSpace(stdErr))
        // {
        //     throw new ApplicationException("Errors captured attempting to run transcoder.");
        // }

        process.WaitForExit();
        return File.OpenRead(_outputFile);
    }

    private ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo()
        {
            FileName = "afconvert",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // set input/output
        startInfo.ArgumentList.Add(_inputFile);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(_outputFile);

        // set format
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add("aac");

        // add all arguments
        foreach (var arg in _arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // toggle verbose
        startInfo.ArgumentList.Add("-v");

        // set aac format
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("m4af");

        return startInfo;
    }

    public Guid CreateHLSTranscode()
    {
        throw new NotImplementedException();
    }
}

[EncoderFrontend("AfConvert", OutputFormat.AAC, Platform.MacOS)]
public class AfConvert : IEncoder
{
    public bool EnsureEncoderExists()
    {
        return CommonEncoderMethods.CheckEncoderExists("afconvert");
    }

    public IArgumentBuilder Configure()
    {
        return new AfConvertBuilder();
    }
}