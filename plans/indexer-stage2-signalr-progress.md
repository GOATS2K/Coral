# Stage 2: SignalR Progress Streaming

**Status:** Deferred until Stage 1 complete
**Priority:** Future enhancement
**Dependencies:** Stage 1 bulk insertion must be complete and validated

## Overview

Add real-time progress updates during indexing via SignalR, enabling the frontend to display live progress of scanning, indexing, and embedding operations.

**Goals:**
- Real-time progress updates for frontend
- Show tracks indexed, embeddings completed, current operation
- Support frontend reconnection (persist active scan state)
- Thread-safe counters for parallel embedding worker

**Architecture Change:**
- Move from event-based to channel-based orchestration
- Add centralized progress tracking (ScanReporter)
- Create IndexerHub for SignalR communication

---

## New Service Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│ API Layer                                                    │
│  - LibraryController → IScanChannel (write scan jobs)       │
│  - IndexerHub (SignalR)                                     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Orchestration Layer (Workers)                               │
│  - ScanWorker: Reads IScanChannel, orchestrates scan        │
│  - FileSystemWorker: Watches files, writes to IScanChannel  │
│  - EmbeddingWorker: Reads IEmbeddingChannel + reports       │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Business Logic Layer (Services)                             │
│  - IIndexerService: Pure transformation (files → entities)  │
│  - IScanReporter: Progress aggregation                      │
│  - ISearchService: Keyword indexing (unchanged)             │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Components

### 1. IScanChannel + ScanJob

**Purpose:** Queue scan operations with context for progress reporting

```csharp
public interface IScanChannel
{
    ChannelWriter<ScanJob> GetWriter();
    ChannelReader<ScanJob> GetReader();
}

public record ScanJob(
    MusicLibrary Library,
    string? SpecificDirectory = null,  // null = full library scan
    bool Incremental = false,
    string? RequestId = null,          // For SignalR progress correlation
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
```

**Registration:**
```csharp
// Program.cs
services.AddSingleton<IScanChannel, ScanChannel>();
```

---

### 2. ScanReporter - Progress Aggregation

**Purpose:** Centralized progress tracking for indexing and embedding operations

**Problem:** IndexerService and EmbeddingWorker need to coordinate progress reporting:
- IndexerService indexes tracks (fast)
- EmbeddingWorker generates embeddings (slow, parallelized)
- Both need to report progress for the same scan job
- EmbeddingWorker processes 10 tracks in parallel → needs thread-safe counters

**Solution:** Singleton state store with thread-safe counters

```csharp
public interface IScanReporter
{
    void RegisterScan(string? requestId, int expectedTracks, MusicLibrary library);
    void ReportTrackIndexed(string? requestId);
    void ReportEmbeddingCompleted(string? requestId);
    ScanJobProgress? GetProgress(string? requestId);
    List<ScanJobProgress> GetActiveScans();  // For frontend reconnection
    void CompleteScan(string? requestId);
}

public class ScanReporter : IScanReporter
{
    private readonly ConcurrentDictionary<string, ScanJobProgress> _scanJobs = new();
    private readonly IHubContext<IndexerHub> _hubContext;
    private readonly ILogger<ScanReporter> _logger;

    public ScanReporter(IHubContext<IndexerHub> hubContext, ILogger<ScanReporter> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public void RegisterScan(string? requestId, int expectedTracks, MusicLibrary library)
    {
        if (requestId == null) return;

        _scanJobs[requestId] = new ScanJobProgress
        {
            RequestId = requestId,
            LibraryId = library.Id,
            LibraryPath = library.LibraryPath,
            ExpectedTracks = expectedTracks,
            TracksIndexed = 0,
            EmbeddingsCompleted = 0,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Registered scan job {RequestId} for library {Library} ({ExpectedTracks} tracks)",
            requestId, library.LibraryPath, expectedTracks);
    }

    public List<ScanJobProgress> GetActiveScans()
    {
        return _scanJobs.Values.ToList();
    }

    public void ReportTrackIndexed(string? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId, out var progress))
        {
            var indexed = Interlocked.Increment(ref progress.TracksIndexed);
            _ = EmitProgress(requestId, progress);
        }
    }

    public void ReportEmbeddingCompleted(string? requestId)
    {
        if (requestId == null) return;

        if (_scanJobs.TryGetValue(requestId, out var progress))
        {
            var completed = Interlocked.Increment(ref progress.EmbeddingsCompleted);
            _ = EmitProgress(requestId, progress);

            // Auto-cleanup when complete
            if (completed == progress.ExpectedTracks)
            {
                _logger.LogInformation("Scan {RequestId} completed: {Tracks} tracks, took {Duration}s",
                    requestId, completed, (DateTime.UtcNow - progress.StartedAt).TotalSeconds);
                _scanJobs.TryRemove(requestId, out _);
            }
        }
    }

    public void CompleteScan(string? requestId)
    {
        // Called when indexing phase completes (before embeddings)
        // Allows UI to show "Indexing complete, generating embeddings..."
    }

    private async Task EmitProgress(string requestId, ScanJobProgress progress)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ScanProgress", new
            {
                RequestId = requestId,
                TracksIndexed = progress.TracksIndexed,
                EmbeddingsCompleted = progress.EmbeddingsCompleted,
                TotalTracks = progress.ExpectedTracks,
                IndexingProgress = progress.ExpectedTracks > 0
                    ? (int)((double)progress.TracksIndexed / progress.ExpectedTracks * 100)
                    : 0,
                EmbeddingProgress = progress.ExpectedTracks > 0
                    ? (int)((double)progress.EmbeddingsCompleted / progress.ExpectedTracks * 100)
                    : 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit scan progress via SignalR");
        }
    }

    public ScanJobProgress? GetProgress(string? requestId)
    {
        return requestId != null && _scanJobs.TryGetValue(requestId, out var progress)
            ? progress
            : null;
    }
}

public class ScanJobProgress
{
    public string RequestId { get; set; } = null!;
    public Guid LibraryId { get; set; }
    public string LibraryPath { get; set; } = null!;
    public int ExpectedTracks;
    public int TracksIndexed;
    public int EmbeddingsCompleted;
    public DateTime StartedAt;
}
```

**Key Design Decisions:**
1. **Thread Safety:** Uses `Interlocked.Increment` for counters modified by parallel EmbeddingWorker
2. **Auto-cleanup:** Removes completed scan jobs automatically when embeddings finish
3. **SignalR Integration:** Emits progress updates immediately when state changes
4. **Nullable RequestId:** FileSystemWatcher scans don't need progress reporting (requestId = null)

**Registration:**
```csharp
// Program.cs
services.AddSingleton<IScanReporter, ScanReporter>();
```

---

### 3. Update EmbeddingChannel - Include RequestId

**Purpose:** Tag tracks with scan job RequestId so EmbeddingWorker can report progress

```csharp
public interface IEmbeddingChannel
{
    ChannelWriter<EmbeddingJob> GetWriter();
    ChannelReader<EmbeddingJob> GetReader();
}

public record EmbeddingJob(
    Track Track,
    string? RequestId = null  // Null for tracks not part of a scan job
);

public class EmbeddingChannel : IEmbeddingChannel
{
    private readonly Channel<EmbeddingJob> _channel;

    public EmbeddingChannel()
    {
        _channel = Channel.CreateUnbounded<EmbeddingJob>();
    }

    public ChannelWriter<EmbeddingJob> GetWriter() => _channel.Writer;
    public ChannelReader<EmbeddingJob> GetReader() => _channel.Reader;
}
```

---

### 4. Update EmbeddingWorker - Report Progress

**Purpose:** Report embedding completion to ScanReporter for progress tracking

```csharp
public class EmbeddingWorker : BackgroundService
{
    private readonly IEmbeddingChannel _channel;
    private readonly IScanReporter _scanReporter;  // NEW
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InferenceService _inferenceService;
    private readonly SemaphoreSlim _semaphore = new(10);

    public EmbeddingWorker(
        IEmbeddingChannel channel,
        IScanReporter scanReporter,  // NEW
        ILogger<EmbeddingWorker> logger,
        IServiceScopeFactory scopeFactory,
        InferenceService inferenceService)
    {
        _channel = channel;
        _scanReporter = scanReporter;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _inferenceService = inferenceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");

        await _inferenceService.EnsureModelExists();

        while (!stoppingToken.IsCancellationRequested)
        {
            await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
            {
                _ = Task.Run(async () => await GetEmbeddings(stoppingToken, job), stoppingToken);
            }
        }

        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(CancellationToken stoppingToken, EmbeddingJob job)
    {
        var sw = Stopwatch.StartNew();
        var track = job.Track;

        switch (track.DurationInSeconds)
        {
            case < 60:
                _logger.LogWarning("Skipping embeddings for {FilePath}, track too short.", track.AudioFile.FilePath);
                _scanReporter.ReportEmbeddingCompleted(job.RequestId);  // Still report completion
                return;
            case > 60 * 15:
                _logger.LogWarning("Skipping embeddings for {FilePath}, track too long.", track.AudioFile.FilePath);
                _scanReporter.ReportEmbeddingCompleted(job.RequestId);  // Still report completion
                return;
        }

        await _semaphore.WaitAsync(stoppingToken);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

            if (context.TrackEmbeddings.Any(a => a.TrackId == track.Id))
            {
                _scanReporter.ReportEmbeddingCompleted(job.RequestId);  // Already exists
                return;
            }

            var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);
            await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
            {
                CreatedAt = DateTime.UtcNow,
                Embedding = new Vector(embeddings),
                TrackId = track.Id
            }, stoppingToken);
            await context.SaveChangesAsync(stoppingToken);

            // Report completion to ScanReporter (thread-safe with Interlocked.Increment)
            _scanReporter.ReportEmbeddingCompleted(job.RequestId);

            _logger.LogInformation("Stored embeddings for {FilePath} in {Time} seconds",
                track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embeddings for track: {Path}", track.AudioFile.FilePath);
            // Still report completion even on error to avoid stuck progress
            _scanReporter.ReportEmbeddingCompleted(job.RequestId);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

**Key changes:**
- Accept `EmbeddingJob` instead of `Track` from channel
- Inject `IScanReporter` dependency
- Call `ReportEmbeddingCompleted(job.RequestId)` after processing (success or failure)
- Uses thread-safe `Interlocked.Increment` inside ScanReporter for parallel safety

---

### 5. Create IndexerHub (SignalR)

**Purpose:** Expose scan operations and progress to frontend

```csharp
public class IndexerHub : Hub
{
    private readonly IScanReporter _scanReporter;
    private readonly IScanChannel _scanChannel;
    private readonly ILogger<IndexerHub> _logger;

    public IndexerHub(
        IScanReporter scanReporter,
        IScanChannel scanChannel,
        ILogger<IndexerHub> logger)
    {
        _scanReporter = scanReporter;
        _scanChannel = scanChannel;
        _logger = logger;
    }

    // Client can call this to get active scans (reconnection support)
    public async Task<List<ScanJobProgress>> GetActiveScans()
    {
        return _scanReporter.GetActiveScans();
    }

    // Client can call this to trigger a manual scan
    public async Task StartLibraryScan(Guid libraryId)
    {
        // Implementation would retrieve library from DB and queue scan
        _logger.LogInformation("Manual scan requested for library {LibraryId}", libraryId);
        // ... queue scan via IScanChannel ...
    }
}
```

**Frontend Integration:**
```typescript
// Example frontend code
const connection = new HubConnectionBuilder()
    .withUrl("/hubs/indexer")
    .withAutomaticReconnect()
    .build();

connection.on("ScanProgress", (progress) => {
    console.log(`Indexing: ${progress.IndexingProgress}%`);
    console.log(`Embeddings: ${progress.EmbeddingProgress}%`);
    // Update UI...
});

// On reconnection, get active scans
connection.onreconnected(async () => {
    const activeScans = await connection.invoke("GetActiveScans");
    // Resume progress display for active scans...
});

await connection.start();
```

**Registration:**
```csharp
// Program.cs
builder.Services.AddSignalR();

// In app configuration
app.MapHub<IndexerHub>("/hubs/indexer");
```

---

## Implementation Checklist

- [ ] Create `IScanChannel`, `ScanChannel`, `ScanJob` types
- [ ] Create `IScanReporter`, `ScanReporter` implementation
- [ ] Update `EmbeddingChannel` to include `RequestId`
- [ ] Update `EmbeddingWorker` to inject and report to `IScanReporter`
- [ ] Create `IndexerHub` for SignalR communication
- [ ] Register all services in DI container
- [ ] Update API controllers to write to `IScanChannel` instead of calling `IndexerService` directly
- [ ] Test SignalR connection and progress updates
- [ ] Test frontend reconnection with `GetActiveScans()`

---

## Testing Strategy

1. **Unit Tests:**
   - Test `ScanReporter` progress tracking with concurrent updates
   - Test `Interlocked.Increment` thread safety
   - Mock `IHubContext` to verify SignalR messages

2. **Integration Tests:**
   - Test full scan flow: API → ScanChannel → ScanWorker → progress updates
   - Test EmbeddingWorker progress reporting
   - Test SignalR hub methods

3. **Frontend Tests:**
   - Test real-time progress updates
   - Test reconnection scenario (simulate network drop)
   - Test multiple concurrent scans

---

## Benefits

1. **Real-time feedback:** Users see progress instead of waiting blindly
2. **Better UX:** Can show which library is being scanned, percentage complete
3. **Reconnection support:** Frontend can resume progress display after disconnect
4. **Decoupled architecture:** Channel-based orchestration is cleaner than events
5. **Thread-safe:** `Interlocked.Increment` ensures accuracy with parallel workers

---

## Migration Notes

- This stage is **additive** - doesn't break existing functionality
- Can implement incrementally (channels first, then progress reporting, then SignalR)
- Old event-based system can coexist during transition
- Once stable, remove `MusicLibraryRegisteredEventEmitter`
