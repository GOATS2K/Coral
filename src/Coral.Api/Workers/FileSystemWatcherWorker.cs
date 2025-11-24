using Coral.Configuration;
using Coral.Configuration.Models;
using Coral.Database;
using Coral.Services.ChannelWrappers;
using Coral.Services.Indexer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Coral.Api.Workers;

/// <summary>
/// Background service that monitors file system changes in music libraries and triggers scans.
/// </summary>
public class FileSystemWatcherWorker : BackgroundService
{
    private readonly IScanChannel _scanChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ServerConfiguration> _serverConfig;
    private readonly ILogger<FileSystemWatcherWorker> _logger;
    private readonly ConcurrentDictionary<Guid, LibraryWatcher> _libraryWatchers = new();
    private readonly int _debounceSeconds;

    public FileSystemWatcherWorker(
        IScanChannel scanChannel,
        IServiceScopeFactory scopeFactory,
        IOptions<ServerConfiguration> serverConfig,
        ILogger<FileSystemWatcherWorker> logger)
    {
        _scanChannel = scanChannel;
        _scopeFactory = scopeFactory;
        _serverConfig = serverConfig;
        _logger = logger;
        _debounceSeconds = serverConfig.Value.FileWatcher?.DebounceSeconds ?? 5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileSystemWatcherWorker started with {DebounceSeconds}s debounce", _debounceSeconds);

        // Initialize watchers for existing libraries
        await InitializeWatchers(stoppingToken);

        // Periodically check for library changes
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await UpdateWatchers(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating file system watchers");
            }
        }

        _logger.LogInformation("FileSystemWatcherWorker stopping...");
        DisposeAllWatchers();
    }

    private async Task InitializeWatchers(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

            // Temporarily bypass WatchForChanges check for testing
            var libraries = await context.MusicLibraries
                //.Where(l => l.WatchForChanges)
                .ToListAsync(cancellationToken);

            foreach (var library in libraries)
            {
                CreateWatcher(library.Id, library.LibraryPath);
            }

            _logger.LogInformation("Initialized {Count} file system watchers", libraries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize file system watchers");
        }
    }

    private async Task UpdateWatchers(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

            var activeLibraries = await context.MusicLibraries
                .ToListAsync(cancellationToken);

            // Remove watchers for deleted libraries (temporarily ignoring WatchForChanges flag)
            var librariesToRemove = _libraryWatchers.Keys
                .Where(id => !activeLibraries.Any(l => l.Id == id))
                .ToList();

            foreach (var libraryId in librariesToRemove)
            {
                RemoveWatcher(libraryId);
            }

            // Add watchers for new libraries (temporarily ignoring WatchForChanges flag)
            var librariesToAdd = activeLibraries
                .Where(l => !_libraryWatchers.ContainsKey(l.Id))
                .ToList();

            foreach (var library in librariesToAdd)
            {
                CreateWatcher(library.Id, library.LibraryPath);
            }

            if (librariesToRemove.Any() || librariesToAdd.Any())
            {
                _logger.LogInformation("Updated watchers: {Added} added, {Removed} removed",
                    librariesToAdd.Count, librariesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update file system watchers");
        }
    }

    private void CreateWatcher(Guid libraryId, string libraryPath)
    {
        try
        {
            // Check if path exists
            if (!Directory.Exists(libraryPath))
            {
                _logger.LogWarning("Library path does not exist, skipping watcher: {Path}", libraryPath);
                return;
            }

            // Check if this is a network path using cross-platform DriveInfo
            if (IsNetworkPath(libraryPath))
            {
                _logger.LogWarning("Library {LibraryId} is on a network path, file watching may be less reliable: {Path}",
                    libraryId, libraryPath);
            }

            var watcher = new LibraryWatcher(libraryId, libraryPath, _debounceSeconds, _scanChannel, _logger);
            if (_libraryWatchers.TryAdd(libraryId, watcher))
            {
                watcher.Start();
                _logger.LogInformation("Started watching library {LibraryId} at {Path}", libraryId, libraryPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create watcher for library {LibraryId} at {Path}", libraryId, libraryPath);
        }
    }

    private void RemoveWatcher(Guid libraryId)
    {
        if (_libraryWatchers.TryRemove(libraryId, out var watcher))
        {
            watcher.Dispose();
            _logger.LogInformation("Stopped watching library {LibraryId}", libraryId);
        }
    }

    private void DisposeAllWatchers()
    {
        foreach (var watcher in _libraryWatchers.Values)
        {
            watcher.Dispose();
        }
        _libraryWatchers.Clear();
    }

    private bool IsNetworkPath(string libraryPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(libraryPath);

            // Check for UNC paths first (Windows)
            if (fullPath.StartsWith(@"\\"))
                return true;

            // Use DriveInfo for mounted drives (cross-platform)
            var rootPath = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(rootPath))
            {
                var driveInfo = new DriveInfo(rootPath);
                return driveInfo.DriveType == DriveType.Network;
            }

            return false;
        }
        catch (ArgumentException)
        {
            // Path might be invalid or a UNC path DriveInfo can't handle
            // Check if it's a UNC path
            return libraryPath.StartsWith(@"\\");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine if path {Path} is a network path", libraryPath);
            return false;
        }
    }

    /// <summary>
    /// Manages FileSystemWatcher for a single music library
    /// </summary>
    private class LibraryWatcher : IDisposable
    {
        private readonly Guid _libraryId;
        private readonly string _libraryPath;
        private readonly int _debounceSeconds;
        private readonly IScanChannel _scanChannel;
        private readonly ILogger _logger;
        private readonly FileSystemWatcher _watcher;
        private readonly ConcurrentDictionary<string, DirectoryEventTracker> _directoryTrackers = new();

        // Get audio extensions from DirectoryScanner's supported formats
        private static readonly string[] AudioExtensions =
        {
            ".flac", ".mp3", ".mp2", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus"
        };

        public LibraryWatcher(
            Guid libraryId,
            string libraryPath,
            int debounceSeconds,
            IScanChannel scanChannel,
            ILogger logger)
        {
            _libraryId = libraryId;
            _libraryPath = libraryPath;
            _debounceSeconds = debounceSeconds;
            _scanChannel = scanChannel;
            _logger = logger;

            _watcher = new FileSystemWatcher(_libraryPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName |
                              NotifyFilters.DirectoryName |
                              NotifyFilters.LastWrite |
                              NotifyFilters.Size,
                InternalBufferSize = 64 * 1024  // 64KB buffer for batch operations
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
        }

        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            try
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

                // Get or create tracker for this directory
                var tracker = _directoryTrackers.GetOrAdd(directory, dir =>
                {
                    var newTracker = new DirectoryEventTracker(
                        dir,
                        _libraryId,
                        _libraryPath,
                        _debounceSeconds,
                        _scanChannel,
                        _logger);

                    // Clean up tracker when it's done
                    newTracker.OnScanQueued += () =>
                    {
                        _directoryTrackers.TryRemove(dir, out _);
                    };

                    return newTracker;
                });

                // Track the event
                tracker.TrackEvent(e.ChangeType, e.FullPath);

                _logger.LogDebug("Tracked {ChangeType} event for {File} in library {LibraryId}",
                    e.ChangeType, e.Name, _libraryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file system event for {Path}", e.FullPath);
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // Check if this is a directory rename
                var isDirectory = Directory.Exists(e.FullPath);

                if (isDirectory)
                {
                    // For directory renames, handle all audio files within
                    HandleDirectoryRename(e);
                    return;
                }

                // Filter for audio files only
                if (!IsAudioFile(e.FullPath))
                    return;

                var directory = Path.GetDirectoryName(e.FullPath) ?? _libraryPath;

                // Get or create tracker for this directory
                var tracker = _directoryTrackers.GetOrAdd(directory, dir =>
                {
                    var newTracker = new DirectoryEventTracker(
                        dir,
                        _libraryId,
                        _libraryPath,
                        _debounceSeconds,
                        _scanChannel,
                        _logger);

                    newTracker.OnScanQueued += () =>
                    {
                        _directoryTrackers.TryRemove(dir, out _);
                    };

                    return newTracker;
                });

                // Track the rename event with both old and new paths
                tracker.TrackEvent(WatcherChangeTypes.Renamed, e.FullPath, e.OldFullPath);

                _logger.LogDebug("Tracked rename event from {OldPath} to {NewPath} in library {LibraryId}",
                    e.OldName, e.Name, _libraryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling rename event from {OldPath} to {NewPath}",
                    e.OldFullPath, e.FullPath);
            }
        }

        private void HandleDirectoryEvent(FileSystemEventArgs e)
        {
            // For directory deletions, trigger a full library scan
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                _logger.LogInformation("Directory {Path} deleted in library {LibraryId}, queuing full library scan",
                    e.FullPath, _libraryId);

                // Get tracker for library root to trigger full scan
                var tracker = _directoryTrackers.GetOrAdd(_libraryPath, dir =>
                {
                    var newTracker = new DirectoryEventTracker(
                        dir,
                        _libraryId,
                        _libraryPath,
                        _debounceSeconds,
                        _scanChannel,
                        _logger);

                    newTracker.OnScanQueued += () =>
                    {
                        _directoryTrackers.TryRemove(dir, out _);
                    };

                    return newTracker;
                });

                tracker.TrackEvent(e.ChangeType, e.FullPath);
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
                // For new directories, track all audio files within
                TrackDirectoryFiles(e.FullPath, e.ChangeType);
            }
        }

        private void HandleDirectoryRename(RenamedEventArgs e)
        {
            _logger.LogInformation("Directory renamed from {OldPath} to {NewPath} in library {LibraryId}",
                e.OldFullPath, e.FullPath, _libraryId);

            // Cancel any pending scans for the old directory
            if (_directoryTrackers.TryRemove(e.OldFullPath, out var oldTracker))
            {
                oldTracker.Dispose();
            }

            // For directory renames, we need to handle this differently:
            // 1. The directory has already been renamed at the filesystem level
            // 2. Files are already at their new location
            // 3. We need to update the database to reflect the new paths

            // Instead of tracking individual file renames (which would fail because old paths don't exist),
            // we should trigger a full scan of the new directory location
            // This will pick up all the files at their new location and update the database accordingly

            try
            {
                var tracker = _directoryTrackers.GetOrAdd(e.FullPath, dir =>
                {
                    var newTracker = new DirectoryEventTracker(
                        dir,
                        _libraryId,
                        _libraryPath,
                        _debounceSeconds,
                        _scanChannel,
                        _logger);

                    newTracker.OnScanQueued += () =>
                    {
                        _directoryTrackers.TryRemove(dir, out _);
                    };

                    return newTracker;
                });

                // Track this as a change event for the entire directory
                // This will trigger a scan of the directory at its new location
                tracker.TrackEvent(WatcherChangeTypes.Changed, e.FullPath);

                _logger.LogInformation("Queued scan for renamed directory {NewPath} (was {OldPath})",
                    e.FullPath, e.OldFullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling directory rename");
            }
        }

        private void TrackDirectoryFiles(string directoryPath, WatcherChangeTypes changeType)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return;

                var audioFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsAudioFile(f))
                    .ToList();

                if (audioFiles.Count > 0)
                {
                    var tracker = _directoryTrackers.GetOrAdd(directoryPath, dir =>
                    {
                        var newTracker = new DirectoryEventTracker(
                            dir,
                            _libraryId,
                            _libraryPath,
                            _debounceSeconds,
                            _scanChannel,
                            _logger);

                        newTracker.OnScanQueued += () =>
                        {
                            _directoryTrackers.TryRemove(dir, out _);
                        };

                        return newTracker;
                    });

                    foreach (var file in audioFiles)
                    {
                        tracker.TrackEvent(changeType, file);
                    }

                    _logger.LogInformation("Tracked {Count} audio files in new directory {Path}",
                        audioFiles.Count, directoryPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating directory {Path}", directoryPath);
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            _logger.LogError(exception, "FileSystemWatcher error for library {LibraryId} at {Path}",
                _libraryId, _libraryPath);

            // Attempt to recover by restarting the watcher asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    Stop();
                    await Task.Delay(1000);  // Brief delay before restart
                    Start();
                    _logger.LogInformation("Restarted FileSystemWatcher for library {LibraryId}", _libraryId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restart FileSystemWatcher for library {LibraryId}", _libraryId);
                }
            });
        }

        private bool IsAudioFile(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return AudioExtensions.Contains(extension);
        }

        public void Dispose()
        {
            Stop();

            foreach (var tracker in _directoryTrackers.Values)
            {
                tracker.Dispose();
            }
            _directoryTrackers.Clear();

            _watcher.Dispose();
        }
    }
}