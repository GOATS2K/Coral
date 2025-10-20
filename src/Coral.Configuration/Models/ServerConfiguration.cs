namespace Coral.Configuration.Models;

public class ServerConfiguration
{
    public const int CurrentVersion = 1;

    public int ConfigVersion { get; set; } = CurrentVersion;
    public DatabaseSettings Database { get; set; } = new();
    public PathSettings Paths { get; set; } = new();
}
