namespace Coral.Database.Models;

public enum DeviceType
{
    Web = 0,
    Native = 1,
    Electron = 2
}

public enum OperatingSystem
{
    Windows = 0,
    MacOS = 1,
    Linux = 2,
    Android = 3,
    iOS = 4
}

public class Device : BaseTable
{
    public string Name { get; set; } = null!;
    public DeviceType Type { get; set; }
    public OperatingSystem OS { get; set; }
    public DateTime LastSeenAt { get; set; }

    // Session info - null TokenId means no active session
    public Guid? TokenId { get; set; }
    public DateTime? SessionExpiresAt { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
