using Coral.Database.Models;
using Coral.Dto.Models;
using Coral.Services.ChannelWrappers;
using System.Collections.Concurrent;

namespace Coral.Api.Workers;

/// <summary>
/// Tracks file system events for a directory and triggers debounced scans.
/// Uses lock-free design suitable for ASP.NET Core background services.
/// </summary>
public class DirectoryEventTracker : IDisposable
{
    private readonly string _directory;
    private readonly Guid _libraryId;
    private readonly string _libraryPath;
    private readonly int _debounceSeconds;
    private readonly IScanChannel _scanChannel;
    private readonly ILogger _logger;

    // Track events without locks
    private readonly ConcurrentBag<FileSystemEvent> _events = new();
    private CancellationTokenSource? _debounceCts;
    private readonly SemaphoreSlim _scanSemaphore = new(1, 1);
    private bool _disposed;

    // Event fired when a scan has been queued
    public event Action? OnScanQueued;

    public DirectoryEventTracker(
        string directory,
        Guid libraryId,
        string libraryPath,
        int debounceSeconds,
        IScanChannel scanChannel,
        ILogger logger)
    {
        _directory = directory;
        _libraryId = libraryId;
        _libraryPath = libraryPath;
        _debounceSeconds = debounceSeconds;
        _scanChannel = scanChannel;
        _logger = logger;
    }

    public void TrackEvent(WatcherChangeTypes changeType, string filePath, string? oldPath = null)
    {
        if (_disposed) return;

        // Add event to concurrent collection (lock-free)
        _events.Add(new FileSystemEvent
        {
            ChangeType = changeType,
            Path = filePath,
            OldPath = oldPath,
            Timestamp = DateTime.UtcNow
        });

        _logger.LogDebug("Tracked {ChangeType} event for {Path} in {Directory}",
            changeType, filePath, _directory);

        // Reset debounce timer using CancellationTokenSource (lock-free)
        var oldCts = _debounceCts;
        _debounceCts = new CancellationTokenSource();
        oldCts?.Cancel();
        oldCts?.Dispose();

        // Start debounce timer
        _ = DebounceAndTriggerScan(_debounceCts.Token);
    }

    private async Task DebounceAndTriggerScan(CancellationToken cancellationToken)
    {
        try
        {
            // Wait for debounce period
            await Task.Delay(TimeSpan.FromSeconds(_debounceSeconds), cancellationToken);

            // Ensure only one scan is triggered at a time
            await _scanSemaphore.WaitAsync(cancellationToken);
            try
            {
                await TriggerScan();
            }
            finally
            {
                _scanSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Timer was reset, this is expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in debounce timer for directory {Directory}", _directory);
        }
    }

    private async Task TriggerScan()
    {
        // Collect all events (empties the bag)
        var events = new List<FileSystemEvent>();
        while (_events.TryTake(out var evt))
        {
            events.Add(evt);
        }

        if (!events.Any())
            return;

        try
        {
            // Analyze events to determine scan strategy
            var hasRenames = events.Any(e => e.ChangeType == WatcherChangeTypes.Renamed && e.OldPath != null);
            var hasDeletes = events.Any(e => e.ChangeType == WatcherChangeTypes.Deleted);
            var hasChanges = events.Any(e => e.ChangeType == WatcherChangeTypes.Changed);
            var hasCreates = events.Any(e => e.ChangeType == WatcherChangeTypes.Created);

            // Check if this is a directory-level operation (many files affected)
            var isDirectoryOperation = events.Count > 10;

            // Determine if we need a full library scan
            var requiresFullScan = false;

            // Check for directory deletion by seeing if parent directories still exist
            if (hasDeletes)
            {
                // If the directory itself no longer exists, we need a full scan
                if (!Directory.Exists(_directory))
                {
                    requiresFullScan = true;
                    _logger.LogInformation("Directory {Directory} was deleted, triggering full library scan", _directory);
                }
            }

            // Handle renames specially to preserve metadata
            if (hasRenames && !requiresFullScan)
            {
                var renames = events
                    .Where(e => e.ChangeType == WatcherChangeTypes.Renamed && e.OldPath != null)
                    .Select(e => new FileRename(e.OldPath!, e.Path))
                    .ToList();

                // Create a rename-specific scan job
                var renameJob = new ScanJob(
                    Library: new MusicLibrary { Id = _libraryId, LibraryPath = _libraryPath },
                    Type: ScanType.Rename,
                    SpecificDirectory: null,
                    Incremental: false,
                    RequestId: Guid.NewGuid(),
                    Trigger: ScanTrigger.FileSystemEvent,
                    Renames: renames
                );

                await _scanChannel.GetWriter().WriteAsync(renameJob);
                _logger.LogInformation("Queued {Count} rename operations for directory {Directory}",
                    renames.Count, _directory);
            }

            // Queue scan for other changes
            if ((hasChanges || hasCreates || hasDeletes) && !requiresFullScan)
            {
                // For FileSystemWatcher events, we use Incremental = false for the specific directory
                // This ensures we catch all changes including files that were just copied and modified
                var scanJob = new ScanJob(
                    Library: new MusicLibrary { Id = _libraryId, LibraryPath = _libraryPath },
                    Type: ScanType.Index,
                    SpecificDirectory: _directory,  // Scan only this directory
                    Incremental: false,  // Force full scan of the directory to catch all changes
                    RequestId: Guid.NewGuid(),
                    Trigger: ScanTrigger.FileSystemEvent,
                    Renames: null
                );

                await _scanChannel.GetWriter().WriteAsync(scanJob);
                _logger.LogInformation(
                    "Queued full directory scan for {Directory} ({EventCount} events: {Creates} creates, {Changes} changes, {Deletes} deletes)",
                    _directory, events.Count,
                    events.Count(e => e.ChangeType == WatcherChangeTypes.Created),
                    events.Count(e => e.ChangeType == WatcherChangeTypes.Changed),
                    events.Count(e => e.ChangeType == WatcherChangeTypes.Deleted));
            }
            else if (requiresFullScan)
            {
                // Full library scan needed
                var scanJob = new ScanJob(
                    Library: new MusicLibrary { Id = _libraryId, LibraryPath = _libraryPath },
                    Type: ScanType.Index,
                    SpecificDirectory: null,  // Full library scan
                    Incremental: false,
                    RequestId: Guid.NewGuid(),
                    Trigger: ScanTrigger.FileSystemEvent,
                    Renames: null
                );

                await _scanChannel.GetWriter().WriteAsync(scanJob);
                _logger.LogInformation("Queued full library scan for library {LibraryId} due to directory deletion", _libraryId);
            }

            // Notify that a scan has been queued for cleanup
            OnScanQueued?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering scan for directory {Directory}", _directory);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
        _scanSemaphore?.Dispose();

        // Clear any remaining events
        while (_events.TryTake(out _)) { }
    }

    private record FileSystemEvent
    {
        public WatcherChangeTypes ChangeType { get; init; }
        public string Path { get; init; } = string.Empty;
        public string? OldPath { get; init; }
        public DateTime Timestamp { get; init; }
    }
}