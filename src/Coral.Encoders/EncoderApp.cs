namespace Coral.Encoders;

public enum Platform
{
    MacOS, Windows, Linux,
}

public enum OutputFormat
{
    AAC, MP3, Ogg, Opus
}

public class EncoderApp
{
    public string Name { get; set; } = null!;
    public OutputFormat OutputFormat { get; set; }
    public List<Platform> SupportedPlatforms { get; set; } = null!;
}