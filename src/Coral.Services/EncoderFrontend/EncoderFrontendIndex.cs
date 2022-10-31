using System.Security.AccessControl;
using Coral.Services.HelperModels;

namespace Coral.Services.EncoderFrontend;

public class EncoderFrontendIndex
{
    private readonly List<EncoderApp> _encoderFrontends;

    public EncoderFrontendIndex()
    {
        _encoderFrontends = new List<EncoderApp>();
        // AAC
        AddAacEncoders();

        // MP3
        _encoderFrontends.Add(new()
        {
            Name = "lame",
            OutputFormat = OutputFormat.MP3,
            SupportedPlatforms = new()
            {
                Platform.Linux, Platform.Windows, Platform.MacOS
            }
        });

        // OGG
        _encoderFrontends.Add(new()
        {
            Name = "oggenc",
            OutputFormat = OutputFormat.Ogg,
            SupportedPlatforms = new()
            {
                Platform.Linux, Platform.Windows, Platform.MacOS
            }
        });

        // Opus
        _encoderFrontends.Add(new()
        {
            Name = "opusenc",
            OutputFormat = OutputFormat.Opus,
            SupportedPlatforms = new()
            {
                Platform.Linux, Platform.Windows, Platform.MacOS
            }
        });
    }
    
    public List<EncoderApp> GetEncodersForPlatform()
    {
        Platform platform;
        if (OperatingSystem.IsMacOS())
        {
            platform = Platform.MacOS;
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = Platform.Linux;
        }
        else if (OperatingSystem.IsWindows())
        {
            platform = Platform.Windows;
        }
        else
        {
            throw new PlatformNotSupportedException($"Coral does not know of any transcoders for your platform");
        }

        return _encoderFrontends.Where(e => e.SupportedPlatforms.Any(sp => sp == platform)).ToList();
    }

    private void AddAacEncoders()
    {
        _encoderFrontends.AddRange(new List<EncoderApp>()
        {
            new()
            {
                Name = "qaac",
                OutputFormat = OutputFormat.AAC,
                SupportedPlatforms = new List<Platform>()
                {
                    Platform.Windows
                }
            },
            new()
            {
                Name = "afconvert",
                OutputFormat = OutputFormat.AAC,
                SupportedPlatforms = new List<Platform>()
                {
                    Platform.MacOS
                }
            },
            new()
            {
                Name = "aac-enc",
                OutputFormat = OutputFormat.AAC,
                SupportedPlatforms = new List<Platform>()
                {
                    Platform.Linux
                }
            }
        });
    }
}