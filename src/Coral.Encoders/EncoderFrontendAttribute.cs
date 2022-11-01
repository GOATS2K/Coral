namespace Coral.Encoders;

public class EncoderFrontendAttribute : Attribute
{
    public string Name;
    public OutputFormat OutputFormat;
    public Platform[] SupportedPlatforms;

    public EncoderFrontendAttribute(string name, OutputFormat outputFormat, params Platform[] supportedPlatforms)
    {
        Name = name;
        OutputFormat = outputFormat;
        SupportedPlatforms = supportedPlatforms;
    }
}