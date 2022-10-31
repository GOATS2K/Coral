using System.Diagnostics;
using Coral.Database.Models;

namespace Coral.Services.EncoderFrontend;

public interface IEncoder
{
    bool EnsureEncoderExists();
    IArgumentBuilder Configure(); 
    Task<Stream> Transcode(Track track, int bitrate);
}