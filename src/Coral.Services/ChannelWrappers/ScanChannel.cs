using System.Threading.Channels;
using Coral.Database.Models;

namespace Coral.Services.ChannelWrappers;

public interface IScanChannel
{
    ChannelWriter<ScanJob> GetWriter();
    ChannelReader<ScanJob> GetReader();
}

public record ScanJob(
    MusicLibrary Library,
    ScanType Type = ScanType.Index,     // Type of scan operation
    string? SpecificDirectory = null,   // null = full library scan (for Index type)
    bool Incremental = false,            // For Index type
    Guid? RequestId = null,              // For SignalR progress correlation
    ScanTrigger Trigger = ScanTrigger.Manual,
    List<FileRename>? Renames = null    // For Rename type
);

public enum ScanType
{
    Index,   // Regular library indexing (full or incremental)
    Rename   // Handle file renames to preserve metadata
}

public enum ScanTrigger
{
    Manual,          // User-requested via API
    FileSystemEvent, // FileSystemWatcher detected change
    LibraryAdded,    // Initial scan after library registration
    Scheduled        // Periodic scheduled scan
}

public record FileRename(
    string OldPath,
    string NewPath
);

public class ScanChannel : IScanChannel
{
    private readonly Channel<ScanJob> _channel;

    public ScanChannel()
    {
        // Consider bounded channel with appropriate capacity
        _channel = Channel.CreateUnbounded<ScanJob>();
    }

    public ChannelWriter<ScanJob> GetWriter() => _channel.Writer;
    public ChannelReader<ScanJob> GetReader() => _channel.Reader;
}
