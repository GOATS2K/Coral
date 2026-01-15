namespace Coral.Dto.RemotePlayback;

/// <summary>
/// Represents a connected remote device
/// </summary>
public class RemoteDeviceDto
{
    /// <summary>
    /// Unique identifier for the device (generated client-side)
    /// </summary>
    public required string DeviceId { get; set; }

    /// <summary>
    /// Human-readable device name (e.g., "Chrome on Windows")
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// Whether this device is currently the active player
    /// </summary>
    public bool IsActivePlayer { get; set; }
}
