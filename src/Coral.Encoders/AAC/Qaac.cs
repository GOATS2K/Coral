namespace Coral.Encoders.AAC;



public class QaacBuilder : IArgumentBuilder
{
    public Guid CreateHLSTranscode()
    {
        throw new NotImplementedException();
    }

    public IArgumentBuilder SetBitrate(int value)
    {
        throw new NotImplementedException();
    }

    public IArgumentBuilder SetDestinationFile(string path)
    {
        throw new NotImplementedException();
    }

    public IArgumentBuilder SetSourceFile(string path)
    {
        throw new NotImplementedException();
    }

    public Stream Transcode()
    {
        throw new NotImplementedException();
    }
}


[EncoderFrontend("Qaac", OutputFormat.AAC, Platform.Windows)]
public class Qaac : IEncoder
{

    public bool EnsureEncoderExists()
    {
        return CommonEncoderMethods.CheckEncoderExists("qaac");
    }

    public IArgumentBuilder Configure()
    {
        return new QaacBuilder();
    }
}