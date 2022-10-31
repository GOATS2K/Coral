using System.Diagnostics;
using Coral.Database.Models;
using Coral.Services.HelperModels;
using Coral.Services.Helpers;

namespace Coral.Services.EncoderFrontend.AAC;

[EncoderFrontend("AfConvert", OutputFormat.AAC, Platform.MacOS)]
public class AfConvert : IEncoder
{
    public bool EnsureEncoderExists()
    {
        return CommonEncoderMethods.CheckEncoderExists("afconvert");
    }

    public async Task<Stream> Transcode(Track track, int bitrate)
    {
        throw new NotImplementedException();
    }
}