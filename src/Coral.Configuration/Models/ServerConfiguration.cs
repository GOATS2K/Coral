using System.Security.Cryptography;

namespace Coral.Configuration.Models;

public class ServerConfiguration
{
    public const int CurrentVersion = 4;

    public int ConfigVersion { get; set; } = CurrentVersion;
    public PathSettings Paths { get; set; } = new();
    public FileWatcherSettings FileWatcher { get; set; } = new();
    public JwtSettings Jwt { get; set; } = new();
    public InferenceSettings Inference { get; set; } = new();
    public ScheduledTaskSettings ScheduledTasks { get; set; } = new();
}

public class FileWatcherSettings
{
    public int DebounceSeconds { get; set; } = 5;
}

public class JwtSettings
{
    public string Secret { get; set; } = GenerateSecret();
    public int SessionExpirationDays { get; set; } = 30;

    private static string GenerateSecret()
    {
        // Generate a 256-bit (32 bytes) cryptographically secure secret
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

public class InferenceSettings
{
    /// <summary>
    /// Maximum number of concurrent Essentia CLI instances for audio embedding extraction.
    /// Default: 4 (conservative to avoid memory issues)
    /// </summary>
    public int MaxConcurrentInstances { get; set; } = 4;
}

public class ScheduledTaskSettings
{
    /// <summary>
    /// Whether to run a full library scan when the application starts.
    /// Default: true
    /// </summary>
    public bool ScanOnStartup { get; set; } = true;

    /// <summary>
    /// Interval in minutes between periodic full library scans.
    /// Set to 0 to disable periodic scans.
    /// Default: 60 (1 hour)
    /// </summary>
    public int LibraryScanIntervalMinutes { get; set; } = 60;
}
