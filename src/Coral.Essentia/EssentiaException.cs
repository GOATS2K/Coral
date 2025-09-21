namespace Coral.Essentia;

public class EssentiaException : Exception
{
    public EssentiaException() { }
    public EssentiaException(string message) : base(message) { }
    public EssentiaException(string message, Exception inner) : base(message, inner) { }
}