using Coral.Database.Models;
using Coral.Services.HelperModels;

namespace Coral.Services.EncoderFrontend.AAC;

[EncoderFrontend("Qaac", OutputFormat.AAC, Platform.Windows)]
public class Qaac : IEncoder
{
    
    public bool EnsureEncoderExists()
    {
        throw new NotImplementedException();
    }

    public IArgumentBuilder Configure()
    {
        throw new NotImplementedException();
    }

    public async Task<Stream> Transcode(Track track, int bitrate)
    {
        throw new NotImplementedException();
    }
}