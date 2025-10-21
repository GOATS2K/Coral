# Scan Progress Broadcasting via WebSocket - Implementation Plan

## Overview

Implement real-time library scan progress broadcasting using **SignalR WebSockets**. Coral's library scanning has two distinct phases that must be tracked separately:

1. **Indexing phase** (~10ms/track, seconds to minutes)
   - Scans directories and reads metadata
   - Adds tracks to database
   - Queues tracks for embedding generation

2. **Embedding generation phase** (~4-6 seconds/track, minutes to hours)
   - Generates ML embeddings via Coral.Essentia.Cli
   - Runs in background (max 10 concurrent)
   - Continues even if user disconnects

**Example: 2,847 track library**
- Indexing: ~30 seconds (2,847 × 10ms)
- Embeddings: ~24 minutes (2,847 × 5s / 10 concurrent)

## Architecture Philosophy

**Self-hosted, keep it simple:**
- Coral is self-hosted (typically 1-10 users, single instance)
- No need for Redis backplanes, load balancing, or complex scaling
- In-memory state is perfectly fine
- Focus on functionality over enterprise-grade scalability

**Build incrementally:**
1. **Phase 1 (this plan):** Anonymous ProgressHub for two-phase scan progress
2. **Phase 2:** Add authentication when auth system is ready
3. **Phase 3:** Add PlaybackHub for remote playback control
4. **Phase 4+:** Additional hubs (transcoding, lyrics, etc.)

---

## Data Models

### Progress DTOs

```csharp
// src/Coral.Dto/ProgressModels/ScanProgressDto.cs

namespace Coral.Dto.ProgressModels;

public class ScanProgressDto
{
    public string ScanId { get; set; } = null!;
    public ScanPhase Phase { get; set; }
    public ScanStatus Status { get; set; }
    public DateTime Timestamp { get; set; }

    // Indexing phase fields
    public Guid? LibraryId { get; set; }
    public string? LibraryPath { get; set; }
    public int? TotalDirectories { get; set; }
    public int? ScannedDirectories { get; set; }
    public int? IndexedTracks { get; set; }
    public string? CurrentDirectory { get; set; }

    // Embedding phase fields
    public int? TotalTracksForEmbedding { get; set; }
    public int? EmbeddingsCompleted { get; set; }
    public int? EmbeddingsInProgress { get; set; }
    public int? EmbeddingsFailed { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

public enum ScanPhase
{
    Indexing,
    Embeddings
}

public enum ScanStatus
{
    Started,
    InProgress,
    Completed,
    Failed
}

public class ScanSummaryDto
{
    public int TotalTracksIndexed { get; set; }
    public int TotalDirectoriesScanned { get; set; }
    public int TotalEmbeddingsGenerated { get; set; }
    public int EmbeddingsFailed { get; set; }
    public TimeSpan IndexingDuration { get; set; }
    public TimeSpan EmbeddingDuration { get; set; }
}
```

---

## Backend Implementation

### 1. ProgressHub (Anonymous)

```csharp
// src/Coral.Api/Hubs/ProgressHub.cs

using Microsoft.AspNetCore.SignalR;

namespace Coral.Api.Hubs;

// Note: No [Authorize] attribute for Phase 1
// Will add authentication in Phase 2
public class ProgressHub : Hub
{
    private readonly ILogger<ProgressHub> _logger;

    public ProgressHub(ILogger<ProgressHub> logger)
    {
        _logger = logger;
    }

    // Client subscribes to a specific scan's progress
    public async Task SubscribeToScan(string scanId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"scan_{scanId}");
        _logger.LogInformation(
            "[ProgressHub] Connection {ConnectionId} subscribed to scan {ScanId}",
            Context.ConnectionId,
            scanId
        );
    }

    // Client unsubscribes from a scan
    public async Task UnsubscribeFromScan(string scanId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"scan_{scanId}");
        _logger.LogInformation(
            "[ProgressHub] Connection {ConnectionId} unsubscribed from scan {ScanId}",
            Context.ConnectionId,
            scanId
        );
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[ProgressHub] Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "[ProgressHub] Client disconnected: {ConnectionId}. Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message
        );
        await base.OnDisconnectedAsync(exception);
    }
}
```

### 2. ProgressBroadcastService

```csharp
// src/Coral.Services/ProgressBroadcastService.cs

using Microsoft.AspNetCore.SignalR;
using Coral.Api.Hubs;
using Coral.Dto.ProgressModels;

namespace Coral.Services;

public interface IProgressBroadcastService
{
    // Indexing phase
    Task BroadcastScanProgress(string scanId, ScanProgressDto progress);
    Task BroadcastIndexingCompleted(string scanId, int tracksIndexed);

    // Embedding phase
    Task BroadcastEmbeddingProgress(string scanId, ScanProgressDto progress);
    Task BroadcastEmbeddingCompleted(string scanId);

    // Overall completion
    Task BroadcastScanCompleted(string scanId, ScanSummaryDto summary);
    Task BroadcastScanFailed(string scanId, string error);
}

public class ProgressBroadcastService : IProgressBroadcastService
{
    private readonly IHubContext<ProgressHub> _hubContext;
    private readonly ILogger<ProgressBroadcastService> _logger;

    public ProgressBroadcastService(
        IHubContext<ProgressHub> hubContext,
        ILogger<ProgressBroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastScanProgress(string scanId, ScanProgressDto progress)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("ScanProgress", progress);

            _logger.LogDebug(
                "[ProgressBroadcast] Indexing progress for {ScanId}: {ScannedDirs}/{TotalDirs} dirs, {Tracks} tracks",
                scanId,
                progress.ScannedDirectories,
                progress.TotalDirectories,
                progress.IndexedTracks
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast scan progress for {ScanId}", scanId);
        }
    }

    public async Task BroadcastIndexingCompleted(string scanId, int tracksIndexed)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("IndexingCompleted", scanId, tracksIndexed);

            _logger.LogInformation(
                "[ProgressBroadcast] Indexing completed for {ScanId}: {Tracks} tracks",
                scanId,
                tracksIndexed
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast indexing completion for {ScanId}", scanId);
        }
    }

    public async Task BroadcastEmbeddingProgress(string scanId, ScanProgressDto progress)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("EmbeddingProgress", progress);

            _logger.LogDebug(
                "[ProgressBroadcast] Embedding progress for {ScanId}: {Completed}/{Total} (in progress: {InProgress})",
                scanId,
                progress.EmbeddingsCompleted,
                progress.TotalTracksForEmbedding,
                progress.EmbeddingsInProgress
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast embedding progress for {ScanId}", scanId);
        }
    }

    public async Task BroadcastEmbeddingCompleted(string scanId)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("EmbeddingCompleted", scanId);

            _logger.LogInformation("[ProgressBroadcast] Embeddings completed for {ScanId}", scanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast embedding completion for {ScanId}", scanId);
        }
    }

    public async Task BroadcastScanCompleted(string scanId, ScanSummaryDto summary)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("ScanCompleted", scanId, summary);

            _logger.LogInformation(
                "[ProgressBroadcast] Scan {ScanId} fully completed: {TracksIndexed} tracks indexed, {EmbeddingsGenerated} embeddings generated, {EmbeddingsFailed} failed",
                scanId,
                summary.TotalTracksIndexed,
                summary.TotalEmbeddingsGenerated,
                summary.EmbeddingsFailed
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast scan completion for {ScanId}", scanId);
        }
    }

    public async Task BroadcastScanFailed(string scanId, string error)
    {
        try
        {
            await _hubContext.Clients
                .Group($"scan_{scanId}")
                .SendAsync("ScanFailed", scanId, error);

            _logger.LogError("[ProgressBroadcast] Scan {ScanId} failed: {Error}", scanId, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProgressBroadcast] Failed to broadcast scan failure for {ScanId}", scanId);
        }
    }
}
```

### 3. ScanStateTracker (In-Memory State)

Tracks scan state across both phases:

```csharp
// src/Coral.Services/ScanStateTracker.cs

using System.Collections.Concurrent;

namespace Coral.Services;

public interface IScanStateTracker
{
    void StartScan(string scanId, int totalTracks);
    void CompleteIndexing(string scanId);
    void IncrementEmbeddingProgress(string scanId);
    void IncrementEmbeddingInProgress(string scanId, int delta);
    void FailEmbedding(string scanId);
    ScanState? GetScanState(string scanId);
    void CompleteScan(string scanId);
}

public class ScanStateTracker : IScanStateTracker
{
    private readonly ConcurrentDictionary<string, ScanState> _scans = new();

    public void StartScan(string scanId, int totalTracks)
    {
        _scans[scanId] = new ScanState
        {
            ScanId = scanId,
            TotalTracks = totalTracks,
            IndexingStartTime = DateTime.UtcNow
        };
    }

    public void CompleteIndexing(string scanId)
    {
        if (_scans.TryGetValue(scanId, out var state))
        {
            state.IndexingCompleted = true;
            state.IndexingEndTime = DateTime.UtcNow;
            state.EmbeddingStartTime = DateTime.UtcNow;
        }
    }

    public void IncrementEmbeddingProgress(string scanId)
    {
        if (_scans.TryGetValue(scanId, out var state))
        {
            Interlocked.Increment(ref state.EmbeddingsCompleted);
        }
    }

    public void IncrementEmbeddingInProgress(string scanId, int delta)
    {
        if (_scans.TryGetValue(scanId, out var state))
        {
            Interlocked.Add(ref state.EmbeddingsInProgress, delta);
        }
    }

    public void FailEmbedding(string scanId)
    {
        if (_scans.TryGetValue(scanId, out var state))
        {
            Interlocked.Increment(ref state.EmbeddingsFailed);
        }
    }

    public ScanState? GetScanState(string scanId)
    {
        _scans.TryGetValue(scanId, out var state);
        return state;
    }

    public void CompleteScan(string scanId)
    {
        if (_scans.TryGetValue(scanId, out var state))
        {
            state.EmbeddingCompleted = true;
            state.EmbeddingEndTime = DateTime.UtcNow;
        }
    }
}

public class ScanState
{
    public string ScanId { get; set; } = null!;
    public int TotalTracks { get; set; }

    public bool IndexingCompleted { get; set; }
    public DateTime IndexingStartTime { get; set; }
    public DateTime? IndexingEndTime { get; set; }

    public int EmbeddingsCompleted { get; set; }
    public int EmbeddingsInProgress { get; set; }
    public int EmbeddingsFailed { get; set; }
    public bool EmbeddingCompleted { get; set; }
    public DateTime? EmbeddingStartTime { get; set; }
    public DateTime? EmbeddingEndTime { get; set; }

    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (!IndexingCompleted || EmbeddingCompleted || EmbeddingsCompleted == 0)
                return null;

            var elapsed = DateTime.UtcNow - EmbeddingStartTime!.Value;
            var avgTimePerTrack = elapsed.TotalSeconds / EmbeddingsCompleted;
            var remaining = TotalTracks - EmbeddingsCompleted - EmbeddingsFailed;
            return TimeSpan.FromSeconds(remaining * avgTimePerTrack);
        }
    }
}
```

### 4. Enhanced IndexerService

```csharp
// src/Coral.Services/IIndexerService.cs (add method signature)

public interface IIndexerService
{
    // ... existing methods ...
    Task ScanLibrariesWithProgress(string scanId);
}
```

```csharp
// src/Coral.Services/IndexerService.cs (modifications)

public class IndexerService : IIndexerService
{
    private readonly IProgressBroadcastService _progressBroadcast;
    private readonly IScanStateTracker _scanStateTracker;

    // AsyncLocal to thread scanId through method calls without changing signatures
    private static readonly AsyncLocal<string?> _currentScanId = new();

    // Update constructor to inject new dependencies
    public IndexerService(
        CoralDbContext context,
        ISearchService searchService,
        ILogger<IndexerService> logger,
        IArtworkService artworkService,
        MusicLibraryRegisteredEventEmitter eventEmitter,
        IMapper mapper,
        IEmbeddingChannel embeddingChannel,
        IProgressBroadcastService progressBroadcast,
        IScanStateTracker scanStateTracker)
    {
        _context = context;
        _searchService = searchService;
        _logger = logger;
        _artworkService = artworkService;
        _musicLibraryRegisteredEventEmitter = eventEmitter;
        _mapper = mapper;
        _embeddingChannel = embeddingChannel;
        _progressBroadcast = progressBroadcast;
        _scanStateTracker = scanStateTracker;
    }

    public async Task ScanLibrariesWithProgress(string scanId)
    {
        _currentScanId.Value = scanId;

        try
        {
            // Start indexing phase
            await _progressBroadcast.BroadcastScanProgress(scanId, new ScanProgressDto
            {
                ScanId = scanId,
                Phase = ScanPhase.Indexing,
                Status = ScanStatus.Started,
                Timestamp = DateTime.UtcNow
            });

            var libraries = _context.MusicLibraries.ToList();
            var totalDirectoriesScanned = 0;
            var totalTracksIndexed = 0;

            foreach (var library in libraries)
            {
                if (library.LibraryPath == "") continue;

                var (tracksIndexed, directoriesScanned) = await ScanLibraryWithProgress(library, scanId);
                totalTracksIndexed += tracksIndexed;
                totalDirectoriesScanned += directoriesScanned;
            }

            // Indexing complete, initialize state tracker for embedding phase
            _scanStateTracker.StartScan(scanId, totalTracksIndexed);
            _scanStateTracker.CompleteIndexing(scanId);

            // Notify indexing completed
            await _progressBroadcast.BroadcastIndexingCompleted(scanId, totalTracksIndexed);

            _logger.LogInformation(
                "Scan {ScanId} indexing complete: {Tracks} tracks from {Dirs} directories. Embeddings will continue in background.",
                scanId,
                totalTracksIndexed,
                totalDirectoriesScanned
            );

            // Note: Embedding phase progress is handled by EmbeddingWorker
            // We don't wait for it here - it runs in background
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed during indexing", scanId);
            await _progressBroadcast.BroadcastScanFailed(scanId, ex.Message);
            throw;
        }
        finally
        {
            _currentScanId.Value = null;
        }
    }

    private async Task<(int tracksIndexed, int directoriesScanned)> ScanLibraryWithProgress(
        MusicLibrary library,
        string scanId)
    {
        library = await GetLibrary(library);
        await DeleteMissingTracks(library);
        var directoryGroups = await ScanMusicLibraryAsync(library, incremental: false);

        var totalDirectories = directoryGroups.Count();
        var scannedDirectories = 0;
        var totalTracks = 0;

        foreach (var directoryGroup in directoryGroups)
        {
            var tracksInDirectory = directoryGroup.ToList();
            if (!tracksInDirectory.Any()) continue;

            await IndexDirectory(tracksInDirectory, library);

            scannedDirectories++;
            totalTracks += tracksInDirectory.Count;

            // Broadcast indexing progress every 5 directories
            if (scannedDirectories % 5 == 0 || scannedDirectories == totalDirectories)
            {
                await _progressBroadcast.BroadcastScanProgress(scanId, new ScanProgressDto
                {
                    ScanId = scanId,
                    Phase = ScanPhase.Indexing,
                    Status = ScanStatus.InProgress,
                    LibraryId = library.Id,
                    LibraryPath = library.LibraryPath,
                    TotalDirectories = totalDirectories,
                    ScannedDirectories = scannedDirectories,
                    IndexedTracks = totalTracks,
                    CurrentDirectory = directoryGroup.Key,
                    Timestamp = DateTime.UtcNow
                });
            }

            // Existing change tracker clear logic
            if (scannedDirectories % 25 == 0)
            {
                _context.ChangeTracker.Clear();
                library = await GetLibrary(library);
            }
        }

        library.LastScan = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (totalTracks, scannedDirectories);
    }

    // Modify existing IndexTrack method to pass scanId to embedding channel
    private async Task<Track> IndexTrack(/* ... existing parameters ... */)
    {
        // ... existing track indexing logic ...

        // Pass scanId along with track to embedding channel
        await _embeddingChannel.GetWriter().WriteAsync((indexedTrack, _currentScanId.Value));

        return indexedTrack;
    }
}
```

### 5. Enhanced EmbeddingChannel

```csharp
// src/Coral.Services/ChannelWrappers/EmbeddingChannel.cs

using System.Threading.Channels;
using Coral.Database.Models;

namespace Coral.Services.ChannelWrappers;

public interface IEmbeddingChannel
{
    ChannelWriter<(Track track, string? scanId)> GetWriter();
    ChannelReader<(Track track, string? scanId)> GetReader();
}

public class EmbeddingChannel : IEmbeddingChannel
{
    private readonly Channel<(Track, string?)> _channel;

    public EmbeddingChannel()
    {
        _channel = Channel.CreateUnbounded<(Track, string?)>();
    }

    public ChannelWriter<(Track, string?)> GetWriter() => _channel.Writer;
    public ChannelReader<(Track, string?)> GetReader() => _channel.Reader;
}
```

### 6. Enhanced EmbeddingWorker

```csharp
// src/Coral.Api/Workers/EmbeddingWorker.cs (modifications)

public class EmbeddingWorker : BackgroundService
{
    private readonly IEmbeddingChannel _channel;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InferenceService _inferenceService;
    private readonly IProgressBroadcastService _progressBroadcast;
    private readonly IScanStateTracker _scanStateTracker;
    private readonly SemaphoreSlim _semaphore = new(10);

    public EmbeddingWorker(
        IEmbeddingChannel channel,
        ILogger<EmbeddingWorker> logger,
        IServiceScopeFactory scopeFactory,
        InferenceService inferenceService,
        IProgressBroadcastService progressBroadcast,
        IScanStateTracker scanStateTracker)
    {
        _channel = channel;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _inferenceService = inferenceService;
        _progressBroadcast = progressBroadcast;
        _scanStateTracker = scanStateTracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");

        await _inferenceService.EnsureModelExists();

        while (!stoppingToken.IsCancellationRequested)
        {
            await foreach (var (track, scanId) in _channel.GetReader().ReadAllAsync(stoppingToken))
            {
                _ = Task.Run(async () => await GetEmbeddings(stoppingToken, track, scanId), stoppingToken);
            }
        }

        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(CancellationToken stoppingToken, Track track, string? scanId)
    {
        var sw = Stopwatch.StartNew();

        // Track in-progress count
        if (scanId != null)
        {
            _scanStateTracker.IncrementEmbeddingInProgress(scanId, 1);
        }

        try
        {
            // Existing validation logic
            switch (track.DurationInSeconds)
            {
                case < 60:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too short.",
                        track.AudioFile.FilePath);
                    if (scanId != null) _scanStateTracker.FailEmbedding(scanId);
                    return;
                case > 60 * 15:
                    _logger.LogWarning("Skipping getting embeddings for track: {FilePath}, track too long.",
                        track.AudioFile.FilePath);
                    if (scanId != null) _scanStateTracker.FailEmbedding(scanId);
                    return;
            }

            await _semaphore.WaitAsync(stoppingToken);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

                if (context.TrackEmbeddings.Any(a => a.TrackId == track.Id))
                    return;

                var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);
                await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
                {
                    CreatedAt = DateTime.UtcNow,
                    Embedding = new Vector(embeddings),
                    TrackId = track.Id
                }, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Stored embeddings for track {FilePath} in {Time} seconds",
                    track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);

                // Update scan state and broadcast progress
                if (scanId != null)
                {
                    _scanStateTracker.IncrementEmbeddingProgress(scanId);
                    _scanStateTracker.IncrementEmbeddingInProgress(scanId, -1);

                    var state = _scanStateTracker.GetScanState(scanId);
                    if (state != null)
                    {
                        // Broadcast every 10 tracks or when nearly complete
                        var shouldBroadcast = state.EmbeddingsCompleted % 10 == 0 ||
                                            state.EmbeddingsCompleted + state.EmbeddingsFailed >= state.TotalTracks - 5;

                        if (shouldBroadcast)
                        {
                            await _progressBroadcast.BroadcastEmbeddingProgress(scanId, new ScanProgressDto
                            {
                                ScanId = scanId,
                                Phase = ScanPhase.Embeddings,
                                Status = ScanStatus.InProgress,
                                TotalTracksForEmbedding = state.TotalTracks,
                                EmbeddingsCompleted = state.EmbeddingsCompleted,
                                EmbeddingsInProgress = state.EmbeddingsInProgress,
                                EmbeddingsFailed = state.EmbeddingsFailed,
                                EstimatedTimeRemaining = state.EstimatedTimeRemaining,
                                Timestamp = DateTime.UtcNow
                            });
                        }

                        // Check if all embeddings complete
                        if (state.EmbeddingsCompleted + state.EmbeddingsFailed >= state.TotalTracks)
                        {
                            _scanStateTracker.CompleteScan(scanId);
                            await _progressBroadcast.BroadcastEmbeddingCompleted(scanId);

                            // Send final summary
                            await _progressBroadcast.BroadcastScanCompleted(scanId, new ScanSummaryDto
                            {
                                TotalTracksIndexed = state.TotalTracks,
                                TotalEmbeddingsGenerated = state.EmbeddingsCompleted,
                                EmbeddingsFailed = state.EmbeddingsFailed,
                                IndexingDuration = (state.IndexingEndTime!.Value - state.IndexingStartTime),
                                EmbeddingDuration = (state.EmbeddingEndTime!.Value - state.EmbeddingStartTime!.Value)
                            });
                        }
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embeddings for track: {Path}", track.AudioFile.FilePath);

            if (scanId != null)
            {
                _scanStateTracker.FailEmbedding(scanId);
                _scanStateTracker.IncrementEmbeddingInProgress(scanId, -1);
            }
        }
    }
}
```

### 7. Update LibraryController

```csharp
// src/Coral.Api/Controllers/LibraryController.cs (modify scan endpoint)

[HttpPost]
[Route("scan")]
public async Task<ActionResult> StartScan()
{
    // Generate unique scan ID
    var scanId = Guid.NewGuid().ToString();

    // Start scan in background (fire and forget)
    _ = Task.Run(async () =>
    {
        try
        {
            await _indexerService.ScanLibrariesWithProgress(scanId);
        }
        catch (Exception ex)
        {
            // Already logged and broadcast in IndexerService
            _logger.LogError(ex, "Background scan {ScanId} failed", scanId);
        }
    });

    return Ok(new { scanId });
}
```

### 8. Configure SignalR in Program.cs

```csharp
// src/Coral.Api/Program.cs

// Add SignalR
builder.Services.AddSignalR();

// Register progress services
builder.Services.AddSingleton<IProgressBroadcastService, ProgressBroadcastService>();
builder.Services.AddSingleton<IScanStateTracker, ScanStateTracker>();

// ... other services ...

// Map SignalR hub
app.MapHub<ProgressHub>("/hubs/progress");
```

---

## Frontend Implementation

### 1. Install SignalR Client

```bash
cd src/coral-app
bun add @microsoft/signalr
```

### 2. Scan Progress Hook

```typescript
// lib/hooks/use-scan-progress.ts

import { useEffect, useState } from 'react';
import * as SignalR from '@microsoft/signalr';
import { baseUrl } from '@/lib/client/fetcher';

export interface ScanProgress {
  scanId: string;
  phase: 'Indexing' | 'Embeddings';
  status: 'Started' | 'InProgress' | 'Completed' | 'Failed';
  timestamp: string;

  // Indexing phase
  libraryPath?: string;
  totalDirectories?: number;
  scannedDirectories?: number;
  indexedTracks?: number;
  currentDirectory?: string;

  // Embedding phase
  totalTracksForEmbedding?: number;
  embeddingsCompleted?: number;
  embeddingsInProgress?: number;
  embeddingsFailed?: number;
  estimatedTimeRemaining?: string; // ISO 8601 duration
}

export interface ScanSummary {
  totalTracksIndexed: number;
  totalDirectoriesScanned: number;
  totalEmbeddingsGenerated: number;
  embeddingsFailed: number;
  indexingDuration: string;
  embeddingDuration: string;
}

export function useScanProgress(scanId: string | null) {
  const [indexingProgress, setIndexingProgress] = useState<ScanProgress | null>(null);
  const [embeddingProgress, setEmbeddingProgress] = useState<ScanProgress | null>(null);
  const [summary, setSummary] = useState<ScanSummary | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!scanId) return;

    let connection: SignalR.HubConnection | null = null;

    async function connect() {
      try {
        // Build connection
        connection = new SignalR.HubConnectionBuilder()
          .withUrl(`${baseUrl}/hubs/progress`)
          .withAutomaticReconnect()
          .configureLogging(SignalR.LogLevel.Information)
          .build();

        // Set up event handlers BEFORE starting connection

        // Indexing phase progress
        connection.on('ScanProgress', (data: ScanProgress) => {
          console.info('[ProgressHub] ScanProgress:', data);
          if (data.phase === 'Indexing') {
            setIndexingProgress(data);
          }
        });

        // Indexing completed
        connection.on('IndexingCompleted', (completedScanId: string, tracksIndexed: number) => {
          console.info('[ProgressHub] IndexingCompleted:', completedScanId, tracksIndexed);
          if (completedScanId === scanId) {
            setIndexingProgress({
              scanId,
              phase: 'Indexing',
              status: 'Completed',
              indexedTracks: tracksIndexed,
              timestamp: new Date().toISOString()
            });
          }
        });

        // Embedding phase progress
        connection.on('EmbeddingProgress', (data: ScanProgress) => {
          console.info('[ProgressHub] EmbeddingProgress:', data);
          setEmbeddingProgress(data);
        });

        // Embeddings completed
        connection.on('EmbeddingCompleted', (completedScanId: string) => {
          console.info('[ProgressHub] EmbeddingCompleted:', completedScanId);
          if (completedScanId === scanId) {
            setEmbeddingProgress(prev => prev ? {
              ...prev,
              status: 'Completed',
              timestamp: new Date().toISOString()
            } : null);
          }
        });

        // Overall scan completed
        connection.on('ScanCompleted', (completedScanId: string, scanSummary: ScanSummary) => {
          console.info('[ProgressHub] ScanCompleted:', completedScanId, scanSummary);
          if (completedScanId === scanId) {
            setSummary(scanSummary);
          }
        });

        // Scan failed
        connection.on('ScanFailed', (failedScanId: string, errorMsg: string) => {
          console.error('[ProgressHub] ScanFailed:', failedScanId, errorMsg);
          if (failedScanId === scanId) {
            setError(errorMsg);
          }
        });

        // Reconnection handling
        connection.onreconnecting((error) => {
          console.warn('[ProgressHub] Reconnecting...', error);
          setIsConnected(false);
        });

        connection.onreconnected(async (connectionId) => {
          console.info('[ProgressHub] Reconnected:', connectionId);
          setIsConnected(true);
          // Re-subscribe after reconnection
          try {
            await connection?.invoke('SubscribeToScan', scanId);
          } catch (err) {
            console.error('[ProgressHub] Re-subscription failed:', err);
          }
        });

        connection.onclose((error) => {
          console.warn('[ProgressHub] Connection closed:', error);
          setIsConnected(false);
        });

        // Start connection
        await connection.start();
        console.info('[ProgressHub] Connected');
        setIsConnected(true);

        // Subscribe to this scan
        await connection.invoke('SubscribeToScan', scanId);
        console.info('[ProgressHub] Subscribed to scan:', scanId);

      } catch (err) {
        console.error('[ProgressHub] Connection error:', err);
        setError(err instanceof Error ? err.message : 'Connection failed');
        setIsConnected(false);
      }
    }

    connect();

    // Cleanup
    return () => {
      if (connection) {
        connection.invoke('UnsubscribeFromScan', scanId).catch(console.error);
        connection.stop().catch(console.error);
      }
    };
  }, [scanId]);

  return { indexingProgress, embeddingProgress, summary, isConnected, error };
}
```

### 3. Music Libraries Hook

```typescript
// lib/hooks/use-music-libraries.ts (add to existing file)

export function useScanLibraries() {
  const [scanId, setScanId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: async () => {
      const response = await client.POST('/api/library/scan');
      return response.data;
    },
    onSuccess: (data) => {
      if (data?.scanId) {
        setScanId(data.scanId);
      }
    },
    onSettled: () => {
      // Invalidate libraries query after scan completes
      queryClient.invalidateQueries(['musicLibraries']);
    }
  });

  return { ...mutation, scanId };
}
```

### 4. UI Component

```typescript
// app/settings/libraries.tsx (example implementation)

import { View } from 'react-native';
import { Text } from '@/components/ui/text';
import { Button } from '@/components/ui/button';
import { useScanProgress } from '@/lib/hooks/use-scan-progress';
import { useScanLibraries } from '@/lib/hooks/use-music-libraries';

export default function LibrariesPage() {
  const { mutate: startScan, scanId, isPending } = useScanLibraries();
  const { indexingProgress, embeddingProgress, summary, isConnected, error } = useScanProgress(scanId);

  const handleScan = () => {
    startScan();
  };

  const getIndexingPercentage = () => {
    if (!indexingProgress?.totalDirectories || !indexingProgress?.scannedDirectories) return 0;
    return Math.round((indexingProgress.scannedDirectories / indexingProgress.totalDirectories) * 100);
  };

  const getEmbeddingPercentage = () => {
    if (!embeddingProgress?.totalTracksForEmbedding || !embeddingProgress?.embeddingsCompleted) return 0;
    return Math.round((embeddingProgress.embeddingsCompleted / embeddingProgress.totalTracksForEmbedding) * 100);
  };

  const formatDuration = (iso8601Duration: string) => {
    // Parse ISO 8601 duration (e.g., "PT1H23M45S") and format as "1h 23m"
    const match = iso8601Duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?/);
    if (!match) return '';

    const hours = match[1] ? `${match[1]}h ` : '';
    const minutes = match[2] ? `${match[2]}m` : '';
    return hours + minutes || '< 1m';
  };

  return (
    <View className="p-4">
      <Text variant="h2" className="mb-4">Music Libraries</Text>

      {/* Scan button */}
      <Button
        onPress={handleScan}
        disabled={isPending || isConnected}
        className="mb-4"
      >
        <Text>{isConnected ? 'Scanning...' : 'Scan All Libraries'}</Text>
      </Button>

      {/* Indexing Phase */}
      {indexingProgress && (
        <View className="mb-4 p-4 border border-border rounded-lg">
          <View className="flex-row items-center mb-2">
            {indexingProgress.status === 'Completed' && (
              <Text className="text-green-500 mr-2">✓</Text>
            )}
            <Text className="font-medium">Phase 1: Indexing</Text>
          </View>

          {indexingProgress.status === 'InProgress' && (
            <>
              <Text className="text-sm text-muted-foreground mb-2">
                {indexingProgress.libraryPath}
              </Text>
              <Text className="text-sm mb-2">
                Directories: {indexingProgress.scannedDirectories} / {indexingProgress.totalDirectories}
                {' '}({getIndexingPercentage()}%)
              </Text>
              <Text className="text-sm mb-3">
                Tracks indexed: {indexingProgress.indexedTracks}
              </Text>

              {/* Progress bar */}
              <View className="h-2 bg-muted rounded-full overflow-hidden">
                <View
                  className="h-full bg-primary transition-all duration-300"
                  style={{ width: `${getIndexingPercentage()}%` }}
                />
              </View>
            </>
          )}

          {indexingProgress.status === 'Completed' && (
            <Text className="text-sm text-green-600 dark:text-green-400">
              {indexingProgress.indexedTracks} tracks indexed
            </Text>
          )}
        </View>
      )}

      {/* Embedding Phase */}
      {embeddingProgress && (
        <View className="mb-4 p-4 border border-border rounded-lg">
          <View className="flex-row items-center mb-2">
            {embeddingProgress.status === 'Completed' && (
              <Text className="text-green-500 mr-2">✓</Text>
            )}
            <Text className="font-medium">Phase 2: Generating Embeddings</Text>
          </View>

          {embeddingProgress.status === 'InProgress' && (
            <>
              <Text className="text-sm mb-2">
                Progress: {embeddingProgress.embeddingsCompleted} / {embeddingProgress.totalTracksForEmbedding} tracks
              </Text>
              <Text className="text-sm mb-2">
                In progress: {embeddingProgress.embeddingsInProgress}
              </Text>
              {embeddingProgress.embeddingsFailed > 0 && (
                <Text className="text-sm text-yellow-600 dark:text-yellow-400 mb-2">
                  Failed: {embeddingProgress.embeddingsFailed} (skipped - too short/long)
                </Text>
              )}
              {embeddingProgress.estimatedTimeRemaining && (
                <Text className="text-sm text-muted-foreground mb-3">
                  Est. time remaining: {formatDuration(embeddingProgress.estimatedTimeRemaining)}
                </Text>
              )}

              {/* Progress bar */}
              <View className="h-2 bg-muted rounded-full overflow-hidden">
                <View
                  className="h-full bg-primary transition-all duration-300"
                  style={{ width: `${getEmbeddingPercentage()}%` }}
                />
              </View>
            </>
          )}

          {embeddingProgress.status === 'Completed' && (
            <Text className="text-sm text-green-600 dark:text-green-400">
              All embeddings generated
            </Text>
          )}
        </View>
      )}

      {/* Final Summary */}
      {summary && (
        <View className="p-4 bg-green-50 dark:bg-green-950 border border-green-200 dark:border-green-800 rounded-lg">
          <Text className="font-medium text-green-700 dark:text-green-300 mb-2">
            Scan Complete!
          </Text>
          <Text className="text-sm text-green-600 dark:text-green-400">
            • {summary.totalTracksIndexed} tracks indexed
          </Text>
          <Text className="text-sm text-green-600 dark:text-green-400">
            • {summary.totalEmbeddingsGenerated} embeddings generated
          </Text>
          {summary.embeddingsFailed > 0 && (
            <Text className="text-sm text-yellow-600 dark:text-yellow-400">
              • {summary.embeddingsFailed} tracks skipped
            </Text>
          )}
        </View>
      )}

      {/* Error State */}
      {error && (
        <View className="p-4 bg-destructive/10 border border-destructive rounded-lg">
          <Text className="font-medium text-destructive mb-1">Scan Failed</Text>
          <Text className="text-sm text-destructive/80">{error}</Text>
        </View>
      )}
    </View>
  );
}
```

---

## Testing Strategy

### 1. Backend Testing (Browser Console)

Test SignalR connection manually:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5031/hubs/progress")
  .build();

connection.on("ScanProgress", (data) => console.log("ScanProgress:", data));
connection.on("IndexingCompleted", (scanId, tracks) => console.log("IndexingCompleted:", scanId, tracks));
connection.on("EmbeddingProgress", (data) => console.log("EmbeddingProgress:", data));
connection.on("EmbeddingCompleted", (scanId) => console.log("EmbeddingCompleted:", scanId));
connection.on("ScanCompleted", (scanId, summary) => console.log("ScanCompleted:", scanId, summary));

await connection.start();
console.log("Connected!");

await connection.invoke("SubscribeToScan", "test-scan-id");
console.log("Subscribed to test-scan-id");

// Then trigger a scan via API and watch the events
```

### 2. Small Library Test (10-20 tracks)

- Verify indexing progress updates every 5 directories
- Verify embedding progress updates every 10 tracks
- Check that both phases complete correctly
- Verify estimated time remaining is reasonable

### 3. Large Library Test (100+ tracks)

- Verify progress broadcast throttling works
- Check UI performance with frequent updates
- Verify estimated time calculation accuracy
- Test concurrent embedding processing (max 10)

### 4. Edge Cases

- Library with no tracks
- Tracks that fail embedding (< 60s or > 15min)
- Network disconnect/reconnect during embedding phase
- Browser close/reopen during scan (should reconnect to in-progress scan)

---

## Implementation Checklist

### Backend
- [ ] Create `src/Coral.Dto/ProgressModels/ScanProgressDto.cs`
- [ ] Create `src/Coral.Api/Hubs/ProgressHub.cs`
- [ ] Create `src/Coral.Services/ProgressBroadcastService.cs`
- [ ] Create `src/Coral.Services/ScanStateTracker.cs`
- [ ] Update `src/Coral.Services/ChannelWrappers/EmbeddingChannel.cs` (add scanId)
- [ ] Update `src/Coral.Services/IIndexerService.cs` (add method signature)
- [ ] Update `src/Coral.Services/IndexerService.cs` (add AsyncLocal, progress broadcasting)
- [ ] Update `src/Coral.Api/Workers/EmbeddingWorker.cs` (add progress broadcasting)
- [ ] Update `src/Coral.Api/Controllers/LibraryController.cs` (scan endpoint returns scanId)
- [ ] Update `src/Coral.Api/Program.cs` (register SignalR, services, map hub)
- [ ] Test with browser console

### Frontend
- [ ] Run `bun add @microsoft/signalr` in coral-app
- [ ] Create `lib/hooks/use-scan-progress.ts`
- [ ] Update `lib/hooks/use-music-libraries.ts` (add useScanLibraries)
- [ ] Update `app/settings/libraries.tsx` (add progress UI)
- [ ] Test connection and subscription
- [ ] Test scan progress updates (both phases)
- [ ] Test completion/error handling
- [ ] Test reconnection after network interruption

---

## Adding Authentication (Phase 2)

When the auth system is ready:

1. Add `[Authorize]` attribute to `ProgressHub`
2. Update SignalR connection to include JWT token:
   ```typescript
   .withUrl(`${baseUrl}/hubs/progress`, {
     accessTokenFactory: async () => await getToken()
   })
   ```
3. Optionally validate user permissions in hub methods

---

## Files to Create/Modify

### Backend (C#)
**New files:**
- `src/Coral.Dto/ProgressModels/ScanProgressDto.cs`
- `src/Coral.Api/Hubs/ProgressHub.cs`
- `src/Coral.Services/ProgressBroadcastService.cs`
- `src/Coral.Services/ScanStateTracker.cs`

**Modified files:**
- `src/Coral.Services/ChannelWrappers/EmbeddingChannel.cs`
- `src/Coral.Services/IIndexerService.cs`
- `src/Coral.Services/IndexerService.cs`
- `src/Coral.Api/Workers/EmbeddingWorker.cs`
- `src/Coral.Api/Controllers/LibraryController.cs`
- `src/Coral.Api/Program.cs`

### Frontend (React Native)
**New files:**
- `lib/hooks/use-scan-progress.ts`

**Modified files:**
- `lib/hooks/use-music-libraries.ts`
- `app/settings/libraries.tsx`

**Dependencies:**
- `@microsoft/signalr`
