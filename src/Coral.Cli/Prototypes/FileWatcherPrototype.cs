using Spectre.Console;

namespace Coral.Cli.Prototypes;

public class FileWatcherPrototype : IDisposable
{
    private readonly string _libraryPath;
    private readonly int _debounceSeconds;
    private readonly IAnsiConsole _console;
    private FileSystemWatcher? _watcher;
    private readonly Dictionary<string, DirectoryEventTracker> _directoryTrackers = new();
    private readonly object _lock = new();

    private static readonly string[] AudioExtensions =
    {
        ".flac", ".mp3", ".m4a", ".aac", ".wav", ".ogg", ".opus", ".ape", ".wv", ".tta"
    };

    public FileWatcherPrototype(string libraryPath, int debounceSeconds, IAnsiConsole console)
    {
        _libraryPath = libraryPath;
        _debounceSeconds = debounceSeconds;
        _console = console;
    }

    public void Start()
    {
        _watcher = new FileSystemWatcher(_libraryPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                          NotifyFilters.DirectoryName |
                          NotifyFilters.LastWrite |
                          NotifyFilters.Size,
            InternalBufferSize = 64 * 1024 // 64KB buffer for batch operations
        };

        // Set up event handlers
        _watcher.Created += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;
        _watcher.Error += OnError;

        _watcher.EnableRaisingEvents = true;

        _console.MarkupLine("[green]✓ File watcher started[/]");
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        lock (_lock)
        {
            foreach (var tracker in _directoryTrackers.Values)
            {
                tracker.Dispose();
            }
            _directoryTrackers.Clear();
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // Check if this is a directory event
        var isDirectory = Directory.Exists(e.FullPath) ||
                         (e.ChangeType == WatcherChangeTypes.Deleted && !Path.HasExtension(e.FullPath));

        if (isDirectory)
        {
            HandleDirectoryEvent(e);
            return;
        }

        // Filter for audio files only
        if (!IsAudioFile(e.FullPath))
            return;

        var directory = Path.GetDirectoryName(e.FullPath) ?? _libraryPath;
        var fileName = Path.GetFileName(e.FullPath);

        // Log the event
        var eventColor = e.ChangeType switch
        {
            WatcherChangeTypes.Created => "green",
            WatcherChangeTypes.Changed => "yellow",
            WatcherChangeTypes.Deleted => "red",
            WatcherChangeTypes.Renamed => "blue",
            _ => "white"
        };

        _console.MarkupLine(
            $"[dim]{DateTime.Now:HH:mm:ss.fff}[/] [{eventColor}]{e.ChangeType,-8}[/] [blue]{Markup.Escape(fileName)}[/] in [dim]{Markup.Escape(directory)}[/]");

        // Get or create tracker for this directory
        DirectoryEventTracker tracker;
        lock (_lock)
        {
            if (!_directoryTrackers.TryGetValue(directory, out tracker!))
            {
                tracker = new DirectoryEventTracker(
                    directory,
                    _debounceSeconds,
                    OnScanTriggered,
                    _console);
                _directoryTrackers[directory] = tracker;
            }
        }

        // Track the event
        tracker.TrackEvent(e.ChangeType, e.FullPath);
    }

    private void HandleDirectoryEvent(FileSystemEventArgs e)
    {
        var directoryName = Path.GetFileName(e.FullPath);
        var parentDirectory = Path.GetDirectoryName(e.FullPath) ?? _libraryPath;

        var eventColor = e.ChangeType switch
        {
            WatcherChangeTypes.Created => "green",
            WatcherChangeTypes.Changed => "yellow",
            WatcherChangeTypes.Deleted => "red",
            WatcherChangeTypes.Renamed => "blue",
            _ => "white"
        };

        _console.MarkupLine(
            $"[dim]{DateTime.Now:HH:mm:ss.fff}[/] [{eventColor}]{e.ChangeType,-8}[/] [cyan]{Markup.Escape("[DIR]")} {Markup.Escape(directoryName)}[/] in [dim]{Markup.Escape(parentDirectory)}[/]");

        // For renamed directories, we need to handle both old and new paths
        if (e is RenamedEventArgs renamedArgs)
        {
            var oldDirectoryName = Path.GetFileName(renamedArgs.OldFullPath);
            _console.MarkupLine(
                $"  [dim]→ Directory renamed from [cyan]{Markup.Escape(oldDirectoryName)}[/] to [cyan]{Markup.Escape(directoryName)}[/][/]");

            // Cancel any pending timer for the old directory path to prevent scan triggers for non-existent paths
            lock (_lock)
            {
                if (_directoryTrackers.TryGetValue(renamedArgs.OldFullPath, out var oldTracker))
                {
                    _console.MarkupLine($"  [dim]→ Cancelled pending scan for old directory path[/]");
                    oldTracker.Dispose();
                    _directoryTrackers.Remove(renamedArgs.OldFullPath);
                }
            }

            // Track the old directory for deletion
            TrackDirectoryFiles(renamedArgs.OldFullPath, WatcherChangeTypes.Deleted);
        }

        // For deleted directories, cancel any pending timer to avoid duplicate scans
        if (e.ChangeType == WatcherChangeTypes.Deleted)
        {
            lock (_lock)
            {
                if (_directoryTrackers.TryGetValue(e.FullPath, out var existingTracker))
                {
                    _console.MarkupLine($"  [dim]→ Cancelled pending scan for deleted directory[/]");
                    existingTracker.Dispose();
                    _directoryTrackers.Remove(e.FullPath);
                }
            }
        }

        // Track files in the directory
        TrackDirectoryFiles(e.FullPath, e.ChangeType);
    }

    private void TrackDirectoryFiles(string directoryPath, WatcherChangeTypes changeType)
    {
        try
        {
            if (!Directory.Exists(directoryPath) && changeType != WatcherChangeTypes.Deleted)
                return;

            // For deleted directories, trigger a full library scan to clean up orphaned tracks
            if (changeType == WatcherChangeTypes.Deleted)
            {
                _console.MarkupLine($"  [dim]→ Will trigger full library scan to clean up orphaned tracks[/]");

                // Get or create tracker for the library root
                DirectoryEventTracker tracker;
                lock (_lock)
                {
                    if (!_directoryTrackers.TryGetValue(_libraryPath, out tracker!))
                    {
                        tracker = new DirectoryEventTracker(
                            _libraryPath,
                            _debounceSeconds,
                            OnScanTriggered,
                            _console);
                        _directoryTrackers[_libraryPath] = tracker;
                    }
                }

                // Track a deletion event for the library root to trigger full scan
                tracker.TrackEvent(changeType, directoryPath);
                return;
            }

            var audioFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => IsAudioFile(f))
                .ToList();

            if (audioFiles.Count > 0)
            {
                _console.MarkupLine($"  [dim]→ Found {audioFiles.Count} audio file(s) in directory[/]");

                // Get or create tracker for this directory
                DirectoryEventTracker tracker;
                lock (_lock)
                {
                    if (!_directoryTrackers.TryGetValue(directoryPath, out tracker!))
                    {
                        tracker = new DirectoryEventTracker(
                            directoryPath,
                            _debounceSeconds,
                            OnScanTriggered,
                            _console);
                        _directoryTrackers[directoryPath] = tracker;
                    }
                }

                foreach (var file in audioFiles)
                {
                    tracker.TrackEvent(changeType, file);
                }
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]ERROR enumerating directory: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private void OnScanTriggered(string directory, int fileCount, List<string> affectedFiles)
    {
        _console.WriteLine();
        _console.MarkupLine($"[bold cyan]▶ WOULD TRIGGER SCAN:[/] [blue]{Markup.Escape(directory)}[/]");
        _console.MarkupLine($"  [dim]Files affected: {fileCount}[/]");

        if (fileCount <= 10)
        {
            foreach (var file in affectedFiles)
            {
                _console.MarkupLine($"    • [dim]{Markup.Escape(Path.GetFileName(file))}[/]");
            }
        }
        else
        {
            foreach (var file in affectedFiles.Take(5))
            {
                _console.MarkupLine($"    • [dim]{Markup.Escape(Path.GetFileName(file))}[/]");
            }
            _console.MarkupLine($"    [dim]... and {fileCount - 5} more files[/]");
        }

        _console.WriteLine();

        // Clean up the tracker
        lock (_lock)
        {
            if (_directoryTrackers.TryGetValue(directory, out var tracker))
            {
                tracker.Dispose();
                _directoryTrackers.Remove(directory);
            }
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _console.MarkupLine($"[red]ERROR: {Markup.Escape(e.GetException()?.Message ?? "Unknown error")}[/]");
    }

    private bool IsAudioFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return AudioExtensions.Contains(extension);
    }

    public void Dispose()
    {
        Stop();
    }
}
