using Spectre.Console;
using System.Collections.Concurrent;

namespace Coral.Cli.Prototypes;

public class DirectoryEventTracker : IDisposable
{
    private readonly string _directory;
    private readonly int _debounceSeconds;
    private readonly Action<string, int, List<string>> _onScanTriggered;
    private readonly IAnsiConsole _console;
    private readonly ConcurrentDictionary<string, WatcherChangeTypes> _affectedFiles = new();
    private Timer? _debounceTimer;
    private readonly object _timerLock = new();

    public DirectoryEventTracker(
        string directory,
        int debounceSeconds,
        Action<string, int, List<string>> onScanTriggered,
        IAnsiConsole console)
    {
        _directory = directory;
        _debounceSeconds = debounceSeconds;
        _onScanTriggered = onScanTriggered;
        _console = console;
    }

    public void TrackEvent(WatcherChangeTypes changeType, string filePath)
    {
        // Track this file
        _affectedFiles.AddOrUpdate(filePath, changeType, (_, _) => changeType);

        lock (_timerLock)
        {
            // Dispose old timer if it exists
            _debounceTimer?.Dispose();

            // Create new timer
            _debounceTimer = new Timer(
                OnTimerElapsed,
                null,
                TimeSpan.FromSeconds(_debounceSeconds),
                Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimerElapsed(object? state)
    {
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }

        // Collect all affected files
        var files = _affectedFiles.Keys.ToList();
        var count = files.Count;

        // Trigger the scan
        _onScanTriggered(_directory, count, files);
    }

    public void Dispose()
    {
        lock (_timerLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
        _affectedFiles.Clear();
    }
}
