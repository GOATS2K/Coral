using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using AutoMapper;
using Coral.Database.Models;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;

namespace Coral.Services.EncoderFrontend.AAC;

public class AfConvertBuilder: IArgumentBuilder
{
    private string _inputFile = string.Empty;
    private string _outputFile = string.Empty;
    private List<string> _arguments;
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
    
    public Process Transcode()
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
        
        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new ApplicationException("Transcoder failed to execute.");
        }

        process.WaitForExit();

        return process;
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