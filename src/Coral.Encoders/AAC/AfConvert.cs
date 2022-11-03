using System.Diagnostics;
using Coral.Encoders.EncodingModels;

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

        var startInfo = BuildArguments();

        //var process = Process.Start(startInfo);
        //if (process == null)
        //{
        //    throw new ApplicationException("Transcoder failed to execute.");
        //}

        // var stdOut = process.StandardOutput.ReadToEnd();
        // var stdErr = process.StandardError.ReadToEnd();
        // if (!string.IsNullOrWhiteSpace(stdErr))
        // {
        //     throw new ApplicationException("Errors captured attempting to run transcoder.");
        // }

        // process.WaitForExit();
        return File.OpenRead(_outputFile);
    }

    public string[] BuildArguments()
    {
        var localArgumentList = new List<string>();

        // set input/output
        localArgumentList.Add(_inputFile);
        localArgumentList.Add("-o");
        localArgumentList.Add(_outputFile);

        // set format
        localArgumentList.Add("-d");
        localArgumentList.Add("aac");

        // add all arguments
        foreach (var arg in _arguments)
        {
            localArgumentList.Add(arg);
        }

        // toggle verbose
        localArgumentList.Add("-v");

        // set aac format
        localArgumentList.Add("-f");
        localArgumentList.Add("m4af");

        return localArgumentList.ToArray();
    }

    public Guid CreateHLSTranscode()
    {
        throw new NotImplementedException();
    }

    public IArgumentBuilder GenerateHLSStream()
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

    public TranscodingJob ConfigureTranscodingJob(TranscodingJobRequest request)
    {
        throw new NotImplementedException();
    }
}