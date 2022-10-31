using System.Diagnostics;
using Coral.Database.Models;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;

namespace Coral.Services.EncoderFrontend.AAC;

public class AfConvertBuilder: IArgumentBuilder
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
        if (string.IsNullOrEmpty(_outputFile))
        {
            _outputFile = Path.GetTempFileName();
        }
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
        
        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new ApplicationException("Transcoder failed to execute.");
        }

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(stdErr))
        {
            throw new ApplicationException("Errors captured attempting to run transcoder.");
        }
        
        while (!File.Exists(_outputFile))
        {
            Thread.Sleep(250);
        }

        // process.WaitForExit();
        
        return new FileStream(_outputFile, FileMode.Open);
    }

    public async Task<Process> TranscodeAsync()
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

    public async Task<Stream> Transcode(Track track, int bitrate)
    {
        throw new NotImplementedException();
    }
}