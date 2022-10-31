using Coral.Database.Models;
using Coral.Services.HelperModels;

namespace Coral.Services.EncoderFrontend.AAC;

[EncoderFrontend("AfConvert", OutputFormat.AAC, Platform.MacOS)]
public class AfConvert : IEncoder
{
    public bool EnsureEncoderExists()
    {
        throw new NotImplementedException();
    }

    public async Task<Stream> Transcode(Track track, int bitrate)
    {
        throw new NotImplementedException();
    }
}