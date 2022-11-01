using System.Diagnostics;

namespace Coral.Services.EncoderFrontend;

public interface IArgumentBuilder
{
    IArgumentBuilder SetBitrate(int value);
    IArgumentBuilder SetSourceFile(string path);
    IArgumentBuilder SetDestinationFile(string path);
    Stream Transcode();
}