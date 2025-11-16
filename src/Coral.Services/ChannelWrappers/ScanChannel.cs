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
    string? SpecificDirectory = null,  // null = full library scan
    bool Incremental = false,
    Guid? RequestId = null,          // For SignalR progress correlation
    ScanTrigger Trigger = ScanTrigger.Manual
);

public enum ScanTrigger
{
    Manual,          // User-requested via API
    FileSystemEvent, // FileSystemWatcher detected change
    LibraryAdded     // Initial scan after library registration
}

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
