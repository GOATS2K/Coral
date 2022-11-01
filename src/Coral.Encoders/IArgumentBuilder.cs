namespace Coral.Encoders;

public interface IArgumentBuilder
{
    IArgumentBuilder SetBitrate(int value);
    IArgumentBuilder SetSourceFile(string path);
    IArgumentBuilder SetDestinationFile(string path);
    Guid CreateHLSTranscode();
    Stream Transcode();
}