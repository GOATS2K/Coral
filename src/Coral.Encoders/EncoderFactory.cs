namespace Coral.Encoders;

public interface IEncoderFactory
{
    public Platform GetPlatform();
    public IEncoder? GetEncoder(OutputFormat format);
}

public class EncoderFactory : IEncoderFactory
{
    // it must be overrideable so it can be mocked
    public virtual Platform GetPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Platform.MacOS;
        }

        if (OperatingSystem.IsLinux())
        {
            return Platform.Linux;
        }

        if (OperatingSystem.IsWindows())
        {
            return Platform.Windows;
        }

        throw new PlatformNotSupportedException("Only Windows, Linux and macOS are currently supported platforms.");
    }

    public IEncoder? GetEncoder(OutputFormat format)
    {
        var assemblies = typeof(IEncoder).Assembly;
        var encoders = assemblies
            .GetTypes()
            // get IEncoder classes
            .Where(x => x.GetInterface(nameof(IEncoder)) != null);

        foreach (var type in encoders)
        {
            var attribute = (EncoderFrontendAttribute)Attribute
                .GetCustomAttribute(type, typeof(EncoderFrontendAttribute))!;
            if (attribute.OutputFormat == format && attribute.SupportedPlatforms.Any(p => p == GetPlatform()))
            {
                return Activator.CreateInstance(type) as IEncoder;
            }
        }

        return null;
    }
}