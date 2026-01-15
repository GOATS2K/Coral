namespace Coral.Dto.RemotePlayback;

/// <summary>
/// Represents the current playback state of an active player
/// </summary>
public class PlaybackStateDto
{
    /// <summary>
    /// The ID of the currently playing track, or null if no track is loaded
    /// </summary>
    public Guid? CurrentTrackId { get; set; }

    /// <summary>
    /// Current playback position in milliseconds
    /// </summary>
    public int PositionMs { get; set; }

    /// <summary>
    /// Whether playback is currently active
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// List of track IDs in the current queue
    /// </summary>
    public List<Guid> Queue { get; set; } = new();

    /// <summary>
    /// Index of the current track in the queue
    /// </summary>
    public int CurrentIndex { get; set; }

    /// <summary>
    /// Whether shuffle mode is enabled
    /// </summary>
    public bool IsShuffled { get; set; }

    /// <summary>
    /// Current repeat mode
    /// </summary>
    public RepeatMode RepeatMode { get; set; }

    /// <summary>
    /// Timestamp when this state was captured
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Playback repeat modes
/// </summary>
public enum RepeatMode
{
    Off,
    All,
    One
}
