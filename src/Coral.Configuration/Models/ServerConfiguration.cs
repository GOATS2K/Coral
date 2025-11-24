namespace Coral.Configuration.Models;

public class ServerConfiguration
{
    public const int CurrentVersion = 1;

    public int ConfigVersion { get; set; } = CurrentVersion;
    public PathSettings Paths { get; set; } = new();
    public FileWatcherSettings FileWatcher { get; set; } = new();
}

public class FileWatcherSettings
{
    public int DebounceSeconds { get; set; } = 5;
}
