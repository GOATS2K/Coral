using Coral.Database.Models;

namespace Coral.Services.EncoderFrontend;

public interface IEncoder
{
    bool EnsureEncoderExists();
    Task<Stream> Transcode(Track track, int bitrate);
}