# Progressive Batching + Async Workers Implementation Plan

**Status**: Planning
**Priority**: High - Enables better indexing architecture and progress reporting
**Dependencies**: SQLite migration complete, DuckDB embeddings implemented

---

## Executive Summary

Implement progressive directory-based batching that flushes entities to the database every 100 directories and queues secondary operations (artwork, keywords, embeddings) to async workers. This approach:

- ✅ **Non-blocking**: ScanWorker doesn't wait for secondary operations
- ✅ **Natural boundaries**: Directory = Album (usually), matches user organization
- ✅ **Memory bounded**: Fixed batch size (100 directories) prevents unbounded memory growth
- ✅ **Progress visibility**: Natural sync points for progress reporting every 100 directories
- ✅ **Parallel pipeline**: Artwork/keywords/embeddings processed concurrently while scanning continues
- ✅ **Good performance**: Batches of 100 directories nearly as fast as one giant bulk insert

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ LibraryController                                                │
│   POST /api/library/scan                                         │
│   └─ Queue ScanJob(libraryId, requestId) → IScanChannel         │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ ScanWorker (Orchestration Layer)                                │
│                                                                  │
│  For each 100 directories:                                       │
│    ├─ Call IndexerService.IndexDirectory()                      │
│    ├─ Accumulate entities in BulkInsertContext                  │
│    ├─ Accumulate DirectoryIndexResults for job queuing          │
│    └─ FLUSH:                                                     │
│         ├─ BulkInsertAsync() → entities get IDs                 │
│         ├─ Directly queue to artwork channel                    │
│         ├─ Directly queue to keyword channel                    │
│         ├─ Directly queue to embedding channel                  │
│         └─ Report progress (non-blocking)                        │
└─────────────────────────────────────────────────────────────────┘
                   │                    │                    │
                   ↓                    ↓                    ↓
       ┌────────────────────┐ ┌────────────────────┐ ┌────────────────────┐
       │ KeywordIndexWorker │ │   ArtworkWorker    │ │  EmbeddingWorker   │
       │   (Sequential)     │ │ (5 concurrent)     │ │ (10 concurrent)    │
       │                    │ │                    │ │                    │
       │ • Track ID         │ │ • Album ID         │ │ • Track ID         │
       │ • Keywords[]       │ │ • Artwork path     │ │ • Audio file path  │
       └────────────────────┘ └────────────────────┘ └────────────────────┘
                   │                    │                    │
                   └────────────────────┴────────────────────┘
                                        ↓
                              Report Progress (SignalR)

┌─────────────────────────────────────────────────────────────────┐
│ IndexerService (Pure Business Logic)                            │
│                                                                  │
│  • IndexDirectory(DirectoryGroup) → DirectoryIndexResult        │
│  • No knowledge of channels, workers, or orchestration           │
│  • DirectoryScanner handles file discovery                      │
└─────────────────────────────────────────────────────────────────┘
```

---

## Phase 1: Channel Infrastructure

### 1.1 ScanChannel (Job Queue)

**File**: `src/Coral.Services/ChannelWrappers/ScanChannel.cs`

```csharp
public interface IScanChannel
{
    void QueueScan(ScanJob job);
    ChannelReader<ScanJob> GetReader();
}

public class ScanChannel : IScanChannel
{
    private readonly Channel<ScanJob> _channel;

    public ScanChannel()
    {
        _channel = Channel.CreateUnbounded<ScanJob>();
    }

    public void QueueScan(ScanJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<ScanJob> GetReader() => _channel.Reader;
}

public record ScanJob(
    MusicLibrary Library,
    string? RequestId = null,  // For progress tracking
    bool Incremental = false
);
```

### 1.2 KeywordIndexChannel

**File**: `src/Coral.Services/ChannelWrappers/KeywordIndexChannel.cs`

```csharp
public interface IKeywordIndexChannel
{
    void QueueKeywordIndexing(KeywordIndexJob job);
    ChannelReader<KeywordIndexJob> GetReader();
}

public class KeywordIndexChannel : IKeywordIndexChannel
{
    private readonly Channel<KeywordIndexJob> _channel;

    public KeywordIndexChannel()
    {
        _channel = Channel.CreateUnbounded<KeywordIndexJob>();
    }

    public void QueueKeywordIndexing(KeywordIndexJob job) =>
        _channel.Writer.TryWrite(job);

    public ChannelReader<KeywordIndexJob> GetReader() => _channel.Reader;
}

public record KeywordIndexJob(
    Guid TrackId,
    string SearchableText,  // From Track.ToString() which handles tokenization
    string? ScanId = null
);
```

### 1.3 ArtworkChannel

**File**: `src/Coral.Services/ChannelWrappers/ArtworkChannel.cs`

```csharp
public interface IArtworkChannel
{
    void QueueArtworkExtraction(ArtworkExtractionJob job);
    ChannelReader<ArtworkExtractionJob> GetReader();
}

public class ArtworkChannel : IArtworkChannel
{
    private readonly Channel<ArtworkExtractionJob> _channel;

    public ArtworkChannel()
    {
        _channel = Channel.CreateUnbounded<ArtworkExtractionJob>();
    }

    public void QueueArtworkExtraction(ArtworkExtractionJob job) =>
        _channel.Writer.TryWrite(job);

    public ChannelReader<ArtworkExtractionJob> GetReader() => _channel.Reader;
}

public record ArtworkExtractionJob(
    Guid AlbumId,
    string ArtworkFilePath,
    string? ScanId = null
);
```

### 1.4 EmbeddingChannel (Update Existing)

**File**: `src/Coral.Services/ChannelWrappers/EmbeddingChannel.cs`

```csharp
public record EmbeddingJob(
    Guid TrackId,
    string AudioFilePath,
    string? ScanId = null
);
```

---

## Phase 2: BulkInsertContext (No Changes Needed)

**File**: `src/Coral.BulkExtensions/BulkInsertContext.cs`

BulkInsertContext only handles database operations - no job tracking.

```csharp
public class BulkInsertContext
{
    private readonly Dictionary<Type, object> _entityCaches;
    private readonly List<Relationship> _relationships;
    private readonly BulkInsertOptions _options;

    public int PendingEntityCount => _entityCaches.Values.Sum(c => c.GetNewEntitiesCount());

    public void AddEntity<T>(T entity) where T : class { ... }
    public async Task BulkInsertAsync() { ... }

    public void ClearBatch()
    {
        foreach (var cache in _entityCaches.Values)
            cache.Clear();

        _relationships.Clear();
    }
}
```

**Separation of Concerns**:
- BulkInsertContext = Database insert operations only
- ScanWorker = Accumulates DirectoryIndexResults and queues jobs to channels
- No mixing of concerns

---

## Phase 3: IndexerService (Pure Business Logic)

**File**: `src/Coral.Services/IndexerService.cs`

```csharp
public interface IIndexerService
{
    // Process a directory group and return all indexed entities
    Task<DirectoryIndexResult> IndexDirectory(
        DirectoryGroup directoryGroup,
        MusicLibrary library);
}

public record DirectoryIndexResult
{
    public List<Track> Tracks { get; init; } = new();
    public Album Album { get; init; } = null!;  // Shared album for directory
    public List<Artist> Artists { get; init; } = new();
    public string? ArtworkPath { get; init; }
}

public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ILogger<IndexerService> _logger;

    public IndexerService(
        CoralDbContext context,
        ILogger<IndexerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DirectoryIndexResult> IndexDirectory(
        DirectoryGroup directoryGroup,
        MusicLibrary library)
    {
        var result = new DirectoryIndexResult
        {
            Tracks = new List<Track>(),
            Artists = new List<Artist>()
        };

        // Process all audio files in the directory group
        foreach (var audioFilePath in directoryGroup.AudioFilePaths)
        {
            var track = await ProcessAudioFile(audioFilePath, library);
            result.Tracks.Add(track);

            // First track's album becomes the directory's album
            if (result.Album == null)
            {
                result.Album = track.Album;
                result.ArtworkPath = track.Album.CoverFilePath;
            }

            // Collect unique artists
            foreach (var artist in track.Artists ?? Enumerable.Empty<Artist>())
            {
                if (!result.Artists.Any(a => a.Id == artist.Id))
                {
                    result.Artists.Add(artist);
                }
            }
        }

        return result;
    }

    private async Task<Track> ProcessAudioFile(string filePath, MusicLibrary library)
    {
        // Existing ProcessAudioFile logic - reads metadata, creates Track entity
        // ...
    }
}
```

**Track.ToString() for Keywords:**

Add to Track entity:

```csharp
public partial class Track
{
    public override string ToString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Title))
            parts.Add(Title);

        if (Album != null && !string.IsNullOrWhiteSpace(Album.Title))
            parts.Add(Album.Title);

        if (Artists?.Any() == true)
            parts.AddRange(Artists.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)));

        return string.Join(" ", parts);
    }
}
```

**Key Points:**
- IndexerService has NO knowledge of channels, workers, or orchestration
- Takes `DirectoryGroup` from DirectoryScanner, returns `DirectoryIndexResult`
- Pure transformation with no side effects
- Keywords calculated via `Track.ToString()` in ScanWorker
- All orchestration happens in ScanWorker

---

## Phase 4: ScanWorker (Orchestration Layer)

**File**: `src/Coral.Api/Workers/ScanWorker.cs`

```csharp
public class ScanWorker : BackgroundService
{
    private readonly IScanChannel _scanChannel;
    private readonly IKeywordIndexChannel _keywordChannel;
    private readonly IArtworkChannel _artworkChannel;
    private readonly IEmbeddingChannel _embeddingChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanReporter _scanReporter;
    private readonly IProgressBroadcastService _progressBroadcast;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(
        IScanChannel scanChannel,
        IKeywordIndexChannel keywordChannel,
        IArtworkChannel artworkChannel,
        IEmbeddingChannel embeddingChannel,
        IServiceScopeFactory scopeFactory,
        IScanReporter scanReporter,
        IProgressBroadcastService progressBroadcast,
        ILogger<ScanWorker> logger)
    {
        _scanChannel = scanChannel;
        _keywordChannel = keywordChannel;
        _artworkChannel = artworkChannel;
        _embeddingChannel = embeddingChannel;
        _scopeFactory = scopeFactory;
        _scanReporter = scanReporter;
        _progressBroadcast = progressBroadcast;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scan worker started!");

        await foreach (var scanJob in _scanChannel.GetReader().ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(async () => await ProcessScan(scanJob, stoppingToken), stoppingToken);
        }

        _logger.LogWarning("Scan worker stopped!");
    }

    private async Task ProcessScan(ScanJob scanJob, CancellationToken ct)
    {
        const int DIRECTORY_BATCH_SIZE = 100; // Flush every 100 directories

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
            var directoryScanner = scope.ServiceProvider.GetRequiredService<IDirectoryScanner>();
            var indexerService = scope.ServiceProvider.GetRequiredService<IIndexerService>();

            var bulkContext = new BulkInsertContext(context, _logger);
            var directoryResults = new List<DirectoryIndexResult>(); // Accumulate for job queuing
            var directoryCount = 0;
            var totalTracks = 0;

            // Register scan for progress tracking
            if (scanJob.RequestId != null)
            {
                _scanReporter.RegisterScan(scanJob.RequestId, scanJob.Library);
            }

            // DirectoryScanner finds directories and files
            await foreach (var directoryGroup in directoryScanner.ScanLibrary(
                scanJob.Library,
                scanJob.Incremental))
            {
                // IndexerService processes the directory group
                var result = await indexerService.IndexDirectory(directoryGroup, scanJob.Library);

                // Add all entities to bulk context
                foreach (var track in result.Tracks)
                {
                    bulkContext.AddEntity(track);
                    totalTracks++;
                }

                bulkContext.AddEntity(result.Album);

                foreach (var artist in result.Artists)
                {
                    bulkContext.AddEntity(artist);
                }

                // Accumulate result for job queuing after flush
                directoryResults.Add(result);
                directoryCount++;

                // Flush every 100 directories
                if (directoryCount % DIRECTORY_BATCH_SIZE == 0)
                {
                    await FlushBatchAndQueueJobs(bulkContext, directoryResults, scanJob.RequestId, directoryCount);
                    _logger.LogInformation("Flushed batch: {Count} directories, {Tracks} tracks",
                        directoryCount, totalTracks);
                }
            }

            // Final flush for remaining directories
            if (bulkContext.PendingEntityCount > 0)
            {
                await FlushBatchAndQueueJobs(bulkContext, directoryResults, scanJob.RequestId, directoryCount);
            }

            // Mark indexing phase complete
            if (scanJob.RequestId != null)
            {
                _scanReporter.CompleteIndexing(scanJob.RequestId);
                await _progressBroadcast.BroadcastIndexingCompleted(
                    scanJob.RequestId,
                    totalTracks);
            }

            _logger.LogInformation(
                "Scan completed: {Directories} directories, {Tracks} tracks indexed",
                directoryCount, totalTracks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed for library {LibraryId}", scanJob.Library.Id);

            if (scanJob.RequestId != null)
            {
                await _progressBroadcast.BroadcastScanFailed(scanJob.RequestId, ex.Message);
            }
        }
    }

    private async Task FlushBatchAndQueueJobs(
        BulkInsertContext bulkContext,
        List<DirectoryIndexResult> directoryResults,
        string? scanId,
        int directoriesProcessed)
    {
        // 1. BULK INSERT - entities get IDs assigned
        await bulkContext.BulkInsertAsync();

        // 2. QUEUE ALL ASYNC JOBS directly to channels (non-blocking - scan continues)

        var totalTracks = 0;
        var totalArtworks = 0;
        var totalEmbeddings = 0;

        foreach (var result in directoryResults)
        {
            // Queue keyword jobs for all tracks
            foreach (var track in result.Tracks)
            {
                _keywordChannel.QueueKeywordIndexing(new KeywordIndexJob(
                    TrackId: track.Id,
                    SearchableText: track.ToString(), // Already tokenized
                    ScanId: scanId
                ));
                totalTracks++;

                // Queue embedding if track is in valid duration range
                if (track.DurationInSeconds is >= 60 and <= 900)
                {
                    _embeddingChannel.QueueEmbeddingGeneration(new EmbeddingJob(
                        TrackId: track.Id,
                        AudioFilePath: track.AudioFile.FilePath,
                        ScanId: scanId
                    ));
                    totalEmbeddings++;
                }
            }

            // Queue artwork job (one per album/directory)
            if (!string.IsNullOrEmpty(result.ArtworkPath))
            {
                _artworkChannel.QueueArtworkExtraction(new ArtworkExtractionJob(
                    AlbumId: result.Album.Id,
                    ArtworkFilePath: result.ArtworkPath,
                    ScanId: scanId
                ));
                totalArtworks++;
            }
        }

        // 3. Report progress (jobs queued, not completed)
        if (scanId != null)
        {
            _scanReporter.ReportBatchIndexed(scanId, totalTracks);
            _scanReporter.ReportKeywordQueued(scanId, totalTracks);
            _scanReporter.ReportArtworkQueued(scanId, totalArtworks);
            _scanReporter.ReportEmbeddingQueued(scanId, totalEmbeddings);

            await _progressBroadcast.BroadcastScanProgress(scanId, new ScanProgressDto
            {
                ScanId = scanId,
                Phase = ScanPhase.Indexing,
                Status = ScanStatus.InProgress,
                DirectoriesScanned = directoriesProcessed,
                TracksIndexed = _scanReporter.GetProgress(scanId).TracksIndexed,
                Timestamp = DateTime.UtcNow
            });
        }

        // 4. Clear batch and continue (non-blocking!)
        bulkContext.ClearBatch();
        directoryResults.Clear();
    }
}
```

---

## Phase 5: Background Workers

### 5.1 KeywordIndexWorker (Sequential Processing)

**File**: `src/Coral.Api/Workers/KeywordIndexWorker.cs`

```csharp
public class KeywordIndexWorker : BackgroundService
{
    private readonly IKeywordIndexChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanReporter _scanReporter;
    private readonly ILogger<KeywordIndexWorker> _logger;

    // No semaphore - process one at a time sequentially

    public KeywordIndexWorker(
        IKeywordIndexChannel channel,
        IServiceScopeFactory scopeFactory,
        IScanReporter scanReporter,
        ILogger<KeywordIndexWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _scanReporter = scanReporter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Keyword index worker started!");

        // Process jobs sequentially - await each one
        await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
        {
            await IndexKeywords(job, stoppingToken);
        }

        _logger.LogWarning("Keyword index worker stopped!");
    }

    private async Task IndexKeywords(KeywordIndexJob job, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

            // Track.ToString() already tokenized, just pass to SearchService
            await searchService.IndexTrackKeywords(job.TrackId, job.SearchableText);

            if (job.ScanId != null)
            {
                _scanReporter.ReportKeywordCompleted(job.ScanId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index keywords for track {TrackId}",
                job.TrackId);
        }
    }
}
```

### 5.2 ArtworkWorker (Concurrent Processing)

**File**: `src/Coral.Api/Workers/ArtworkWorker.cs`

```csharp
public class ArtworkWorker : BackgroundService
{
    private readonly IArtworkChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanReporter _scanReporter;
    private readonly ILogger<ArtworkWorker> _logger;
    private readonly SemaphoreSlim _semaphore = new(5); // 5 concurrent

    public ArtworkWorker(
        IArtworkChannel channel,
        IServiceScopeFactory scopeFactory,
        IScanReporter scanReporter,
        ILogger<ArtworkWorker> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _scanReporter = scanReporter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Artwork worker started!");

        await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
        {
            // Process multiple jobs concurrently
            _ = Task.Run(async () => await ProcessArtwork(job, stoppingToken), stoppingToken);
        }

        _logger.LogWarning("Artwork worker stopped!");
    }

    private async Task ProcessArtwork(ArtworkExtractionJob job, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            // Each task gets its own scope = own DbContext (safe for concurrency)
            await using var scope = _scopeFactory.CreateAsyncScope();
            var artworkService = scope.ServiceProvider.GetRequiredService<IArtworkService>();

            await artworkService.ExtractAndProcessArtworkForAlbum(
                job.AlbumId,
                job.ArtworkFilePath);

            _logger.LogInformation("Processed artwork for album {AlbumId}", job.AlbumId);

            if (job.ScanId != null)
            {
                _scanReporter.ReportArtworkCompleted(job.ScanId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process artwork for album {AlbumId}",
                job.AlbumId);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### 5.3 EmbeddingWorker (Concurrent Processing)

**File**: `src/Coral.Api/Workers/EmbeddingWorker.cs` (already exists, verify pattern)

```csharp
public class EmbeddingWorker : BackgroundService
{
    private readonly IEmbeddingChannel _channel;
    private readonly ILogger<EmbeddingWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InferenceService _inferenceService;
    private readonly IEmbeddingService _embeddingService;
    private readonly SemaphoreSlim _semaphore = new(10); // 10 concurrent

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding worker started!");
        await _inferenceService.EnsureModelExists();

        await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(async () => await GetEmbeddings(job, stoppingToken), stoppingToken);
        }

        _logger.LogWarning("Embedding worker stopped!");
    }

    private async Task GetEmbeddings(EmbeddingJob job, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();

            // Check if already exists
            if (await _embeddingService.HasEmbeddingAsync(job.TrackId))
            {
                if (job.ScanId != null)
                    reporter.ReportEmbeddingCompleted(job.ScanId);
                return;
            }

            // Generate and store embeddings
            var embeddings = await _inferenceService.RunInference(job.AudioFilePath);
            await _embeddingService.InsertEmbeddingAsync(job.TrackId, embeddings);

            _logger.LogInformation("Stored embeddings for track {TrackId}", job.TrackId);

            if (job.ScanId != null)
            {
                reporter.ReportEmbeddingCompleted(job.ScanId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for track {TrackId}",
                job.TrackId);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

## Phase 6: LibraryController Integration

**File**: `src/Coral.Api/Controllers/LibraryController.cs`

```csharp
[HttpPost]
[Route("scan")]
public async Task<ActionResult> StartScan()
{
    var scanId = Guid.NewGuid().ToString();

    // Queue scan job (non-blocking)
    var libraries = await _context.MusicLibraries.ToListAsync();
    foreach (var library in libraries)
    {
        _scanChannel.QueueScan(new ScanJob(
            Library: library,
            RequestId: scanId,
            Incremental: false
        ));
    }

    return Ok(new { scanId });
}
```

---

## Phase 7: Service Registration

**File**: `src/Coral.Api/ServiceCollectionExtensions.cs`

```csharp
// Add channels
services.AddSingleton<IScanChannel, ScanChannel>();
services.AddSingleton<IKeywordIndexChannel, KeywordIndexChannel>();
services.AddSingleton<IArtworkChannel, ArtworkChannel>();
// IEmbeddingChannel already registered
```

**File**: `src/Coral.Api/Program.cs`

```csharp
// Add workers
builder.Services.AddHostedService<ScanWorker>();
builder.Services.AddHostedService<KeywordIndexWorker>();
builder.Services.AddHostedService<ArtworkWorker>();
builder.Services.AddHostedService<EmbeddingWorker>();
```

---

## Implementation Checklist

### Phase 1: Channels
- [ ] Create `IScanChannel` + `ScanChannel`
- [ ] Create `IKeywordIndexChannel` + `KeywordIndexChannel`
- [ ] Create `IArtworkChannel` + `ArtworkChannel`
- [ ] Update `IEmbeddingChannel` with new `EmbeddingJob` record

### Phase 2: BulkInsertContext
- [x] No changes needed - only handles database operations

### Phase 3: IndexerService & Track Entity
- [ ] Create `DirectoryIndexResult` record
- [ ] Update `IIndexerService.IndexDirectory()` signature
- [ ] Remove all orchestration logic (channels, workers, directory scanning)
- [ ] Ensure pure business logic only
- [ ] Add `Track.ToString()` override for keyword generation

### Phase 4: ScanWorker
- [ ] Create `ScanWorker` class
- [ ] Inject `IDirectoryScanner` and `IIndexerService`
- [ ] Implement `ProcessScan()` orchestration (calls scanner → indexer)
- [ ] Accumulate `DirectoryIndexResult` objects in a list
- [ ] Implement directory batching logic (flush every 100 directories)
- [ ] Implement `FlushBatchAndQueueJobs()` - directly write to channels
- [ ] Pass `track.ToString()` to `KeywordIndexJob` (no manual tokenization needed)
- [ ] Integrate with progress reporting

### Phase 5: Workers
- [ ] Create `KeywordIndexWorker` (sequential)
- [ ] Create `ArtworkWorker` (5 concurrent)
- [ ] Verify `EmbeddingWorker` matches pattern (10 concurrent)

### Phase 6: Controller
- [ ] Update `LibraryController.StartScan()` to queue to channel
- [ ] Return `scanId` immediately (non-blocking)

### Phase 7: Registration
- [ ] Register all channels in DI
- [ ] Register all workers as hosted services

### Phase 8: Testing
- [ ] Test with small library (10-20 directories)
- [ ] Verify batch flushing at 100 directory intervals
- [ ] Verify all workers process jobs correctly
- [ ] Test progress reporting updates
- [ ] Test with large library (1000+ directories)

---

## Performance Expectations

### Directory-Based Batching Benefits

| Aspect | Track-Based (500) | Directory-Based (100) | Better? |
|--------|------------------|----------------------|---------|
| Natural boundaries | Arbitrary | Album boundaries | ✅ Directory |
| Artwork deduplication | Per-batch | Per-directory (simpler) | ✅ Directory |
| Progress intuition | "500 tracks" | "100 albums" | ✅ Directory |
| User organization | Doesn't match | Matches folder structure | ✅ Directory |

### Batch Insert Performance

| Library Size | Directories | Single Giant Batch | Progressive (100/batch) | Impact |
|--------------|-------------|-------------------|------------------------|--------|
| Small (1,000 tracks) | ~100 | ~3s | ~3-4s | Negligible |
| Medium (10,000 tracks) | ~1,000 | ~8s | ~10-12s | 20-30% slower, acceptable |
| Large (50,000 tracks) | ~5,000 | ~40s | ~50-60s | 20-30% slower, acceptable |

**Note**: Scanning/metadata extraction is the real bottleneck (~10ms/track), not bulk insert.

### Worker Throughput

| Worker | Throughput | Bottleneck | Concurrency |
|--------|-----------|------------|-------------|
| **Keyword** | ~100 tracks/sec | DB insertion | Sequential (1) |
| **Artwork** | ~5-10 albums/sec | I/O (image processing) | Concurrent (5) |
| **Embedding** | ~2 tracks/sec | Inference (CPU/GPU) | Concurrent (10) |

### Expected Timeline for 2,847 Track Library (~285 Albums/Directories)

- **Indexing**: ~30 seconds (3 bulk inserts of ~100 directories each)
- **Keywords**: ~30 seconds (sequential processing, overlaps with indexing)
- **Artwork**: ~2-5 minutes (concurrent processing)
- **Embeddings**: ~24 minutes (concurrent processing, slowest operation)

All operations run in parallel pipelines, so total time ≈ slowest operation (embeddings).

---

## Concurrency Safety

### Safe Pattern (Each Task Has Own Scope)

```csharp
// ✅ SAFE: Each task gets its own DbContext via CreateAsyncScope()
await foreach (var job in _channel.GetReader().ReadAllAsync(stoppingToken))
{
    _ = Task.Run(async () =>
    {
        await using var scope = _scopeFactory.CreateAsyncScope(); // New scope!
        var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
        // ... do work
    }, stoppingToken);
}
```

### Unsafe Pattern (Shared DbContext)

```csharp
// ❌ UNSAFE: Multiple operations on same DbContext instance
await using var scope = _scopeFactory.CreateAsyncScope();
var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

var tasks = jobs.Select(job => ProcessJob(job, context)); // Same context!
await Task.WhenAll(tasks); // 💥 EF Core change tracker conflicts
```

**Key Insight**: The issue is `Task.WhenAll` with a shared DbContext, not concurrent operations with separate scopes.

---

## Worker Concurrency Levels

| Worker | Why This Level? |
|--------|----------------|
| **KeywordIndexWorker (1)** | Business requirement - keywords must be inserted sequentially. Fast enough (~10ms/track) that queue won't back up. |
| **ArtworkWorker (5)** | I/O bound (image processing ~100-500ms). Limit to 5 to avoid overwhelming file system. Each has own DbContext scope = safe. |
| **EmbeddingWorker (10)** | Very slow (inference ~5s/track). Max out concurrency to minimize total time. Each has own DuckDB connection = safe. |

---

## Benefits Summary

1. **Clean separation of concerns**:
   - BulkInsertContext = Database operations only
   - IndexerService = Pure business logic (transform DirectoryGroup → DirectoryIndexResult)
   - ScanWorker = Orchestration (accumulate, flush, queue jobs)
   - No mixing of responsibilities

2. **Non-blocking indexer**: Main scan loop doesn't wait for secondary operations

3. **Memory bounded**: Fixed batch size (100 directories) prevents OOM on large libraries

4. **Natural boundaries**: Directory = album (usually), matches user organization

5. **Better deduplication**: All tracks in directory share same album/artwork

6. **Progress visibility**: Batch flushes = sync points for updates

7. **Parallel pipeline**: Scanning, keyword indexing, artwork extraction, and embedding generation all happen simultaneously

8. **Incremental results**: Users see albums appearing in library as batches complete

9. **Configurable**: Tune directory batch size and worker concurrency per environment

10. **Simple job queuing**: Just write to channel - no intermediate metadata tracking

---

## Future Enhancements

Once stable:

1. **Incremental scanning**: Only process changed directories (file system watcher integration)
2. **Smart batch sizing**: Adjust based on available memory and directory sizes
3. **Priority queues**: Process popular albums/artists first
4. **Retry logic**: Automatic retry for failed jobs with exponential backoff
5. **Job persistence**: Save pending jobs to disk for crash recovery
6. **Metrics**: Track throughput, queue depth, worker utilization
7. **Adaptive concurrency**: Auto-tune worker limits based on system load
