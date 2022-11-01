namespace Coral.Encoders;

public interface IEncoder
{
    bool EnsureEncoderExists();
    IArgumentBuilder Configure();
}