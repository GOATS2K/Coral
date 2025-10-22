# IndexerService Refactoring Plan

## Overview

Refactor IndexerService to support SignalR progress streaming while improving architecture through better separation of concerns, moving from event-based to channel-based orchestration, and improving FileSystemWatcher reliability.

## Current State Analysis

### IndexerService (670 lines)

**Current Dependencies:**
- `CoralDbContext` - Database access ✅ Core
- `IArtworkService` - Artwork extraction/processing ✅ Core
- `ILogger<IndexerService>` - Logging ✅ Core
- `ISearchService` - Keyword indexing (line 431)
- `IEmbeddingChannel` - Queue tracks for ML processing (line 476)
- `MusicLibraryRegisteredEventEmitter` - Event emission (line 80)
- `IMapper` - DTO projection (line 58)

**Responsibilities:**
- Library/directory scanning (full and incremental)
- Track metadata extraction and parsing
- Album/artist/genre creation and management
- File/directory rename handling
- Track deletion and cleanup
- **Side effects:** Writing to embedding channel, search indexing, event emission

### IndexerWorker

**Current Issues:**
- Uses `MemoryCache` with 250ms expiration as debouncing hack
- Async void event handlers (swallows exceptions)
- Semaphore processes scans sequentially (no parallelism)
- No progress reporting capability
- FileSystemWatcher doesn't verify files are fully written
- Couples file watching directly to IndexerService

### Tests

**Current Issues:**
- Each test requires separate Postgres container (slow)
- Tests share/overlap test data causing conflicts
- Line 24 comment: "These tests will overlap, so using ContainerTest makes sure we create a new Postgres database for each test"

## Problems Identified

### 1. Coupling & Single Responsibility Violations

IndexerService mixes:
- Core indexing logic (file metadata → database entities)
- Orchestration (search indexing, embedding queue, events)
- Library management (AddMusicLibrary with DTO projection)

This makes it:
- Hard to test in isolation
- Difficult to add progress reporting
- Unclear ownership of side effects

### 2. Event-Based Architecture Limitations

`MusicLibraryRegisteredEventEmitter` pattern:
- No natural backpressure or queuing
- Can't easily report progress
- Event handlers scattered across workers
- Testing requires mocking event system

### 3. FileSystemWatcher Reliability

Current approach:
- 250ms cache expiration doesn't guarantee file is fully written
- Can still read partial files (hence retry loop in `ReadTracksInDirectory:322-357`)
- Arbitrary timeout - too short for large files, too long for small ones

### 4. No Progress Reporting

IndexerService has no way to report:
- Number of directories scanned
- Current file being processed
- Tracks indexed vs. total
- Errors encountered during scan

### 5. Performance - N+1 Query Problem

**Current Performance:**
- 7,000 tracks: ~10 minutes
- 50,000 tracks: Unacceptably slow (hours?)

**Root Cause Analysis:**

The indexing process performs excessive database queries due to entity lookups on every iteration:

```csharp
// Called for EVERY track (7000 times)
private async Task<Genre> GetGenre(string genreName)
{
    var indexedGenre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == genreName);
    // Line 520: SaveChangesAsync called INSIDE GetArtist for each new artist!
}

// Called for EVERY artist encountered (could be 3-5 per track)
private async Task<Artist> GetArtist(string artistName)
{
    var indexedArtist = _context.Artists.FirstOrDefault(a => a.Name == artistName);
    if (indexedArtist == null)
    {
        _context.Artists.Add(indexedArtist);
        await _context.SaveChangesAsync();  // 🔴 DB round trip for EACH new artist
    }
}

// Called for EVERY track
private async Task<Album> GetAlbum(List<ArtistWithRole> artists, ATL.Track atlTrack)
{
    var albumQuery = _context.Albums
        .Include(a => a.Artists)
        .Include(a => a.Tracks)
        .Where(a => a.Name == albumName && ...);
    var indexedAlbum = await albumQuery.FirstOrDefaultAsync();  // 🔴 Complex query with joins
}

// Called for EVERY track
private async Task<Track> AddOrUpdateTrack(...)
{
    var indexedTrack = await _context.Tracks
        .Include(t => t.AudioFile)
        .Include(t => t.Keywords)
        .Include(t => t.Artists)
        .Include(t => t.Album)
        .FirstOrDefaultAsync(t => t.AudioFile.FilePath == atlTrack.Path);  // 🔴 Query per track
}
```

**Query Count Estimate for 7,000 Track Library:**

Assuming:
- 500 unique artists
- 100 unique genres
- 700 albums
- 50 record labels
- Average 3 artists per track (main + featuring + remixer)

Database round trips:
- **7,000** queries to check if track exists (`AddOrUpdateTrack`)
- **7,000** queries for genre lookups (`GetGenre`)
- **~500** artist queries + **500 SaveChanges** inside `GetArtist` 🔴
- **~1,500** queries for `ArtistsWithRoles` lookups (3 per track)
- **7,000** album queries with joins (`GetAlbum`)
- **~50** record label queries

**Total: ~16,000+ database round trips** for a single library scan

At 50,000 tracks, this becomes **~115,000 queries** - explaining the extreme slowness.

**Additional Issues:**

1. **Change tracker overhead**: Line 129 clears every 25 directories, but still accumulates entities between clears
2. **No batching**: Individual `Add()` calls instead of `AddRange()`
3. **Repeated Include queries**: Same navigation properties loaded repeatedly
4. **No pre-loading**: Reference data loaded on-demand instead of upfront

## Proposed Architecture

### New Service Boundaries

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
│  - EmbeddingWorker: Reads IEmbeddingChannel (unchanged)     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Business Logic Layer (Services)                             │
│  - IIndexerService: Pure transformation (files → entities)  │
│  - ILibraryService: Library CRUD operations                 │
│  - ISearchService: Keyword indexing (unchanged)             │
└─────────────────────────────────────────────────────────────┘
```

### 1. Create IScanChannel + ScanJob

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

### 2. Create ScanReporter - Progress Aggregation

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
    List<ScanJobProgress> GetActiveScans();  // NEW: For frontend reconnection
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

### 3. Create DirectoryScanner - File System Scanning

**Purpose:** Separate file system scanning from indexing logic for better testability and reusability

**Responsibilities:**
- Scan library directory for audio files
- Filter by file extensions
- Handle incremental scan logic (modified/new files only)
- Group files by directory
- Memory-efficient streaming (no loading entire library into memory)

```csharp
public interface IDirectoryScanner
{
    Task<int> CountFiles(MusicLibrary library, bool incremental = false);
    IAsyncEnumerable<DirectoryGroup> ScanLibrary(MusicLibrary library, bool incremental = false);
    Task DeleteMissingTracks(MusicLibrary library);
}

public record DirectoryGroup(
    string DirectoryPath,
    List<FileInfo> Files
);

public class DirectoryScanner : IDirectoryScanner
{
    private readonly CoralDbContext _context;
    private readonly IArtworkService _artworkService;
    private readonly ILogger<DirectoryScanner> _logger;

    private static readonly string[] AudioFileFormats =
        [".flac", ".mp3", ".mp2", ".wav", ".m4a", ".ogg", ".alac", ".aif", ".opus"];

    public DirectoryScanner(
        CoralDbContext context,
        IArtworkService artworkService,
        ILogger<DirectoryScanner> logger)
    {
        _context = context;
        _artworkService = artworkService;
        _logger = logger;
    }

    public Task<int> CountFiles(MusicLibrary library, bool incremental = false)
    {
        var contentDirectory = new DirectoryInfo(library.LibraryPath);

        if (!incremental)
        {
            // Count all audio files in library
            return Task.FromResult(contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Count(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName))));
        }
        else
        {
            // Count only new/modified files since last scan
            return Task.FromResult(contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Count(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           (f.LastWriteTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan)));
        }
    }

    public async IAsyncEnumerable<DirectoryGroup> ScanLibrary(
        MusicLibrary library,
        bool incremental = false)
    {
        var contentDirectory = new DirectoryInfo(library.LibraryPath);

        if (!incremental)
        {
            _logger.LogInformation("Starting full scan of directory: {Directory}", library.LibraryPath);
            var existingFiles = await _context.AudioFiles
                .Where(f => f.Library.Id == library.Id)
                .ToListAsync();

            // Group files by directory, yield each group
            var groups = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           !existingFiles.Any(ef => ef.FilePath == f.FullName &&
                                                    f.LastWriteTimeUtc == ef.UpdatedAt))
                .GroupBy(f => f.Directory?.FullName ?? "");

            foreach (var group in groups)
            {
                yield return new DirectoryGroup(group.Key, group.ToList());
            }
        }
        else
        {
            _logger.LogInformation("Starting incremental scan of directory: {Directory}", library.LibraryPath);

            var groups = contentDirectory
                .EnumerateFiles("*.*", SearchOption.AllDirectories)
                .Where(f => AudioFileFormats.Contains(Path.GetExtension(f.FullName)) &&
                           (f.LastWriteTimeUtc > library.LastScan || f.CreationTimeUtc > library.LastScan))
                .GroupBy(f => f.Directory?.FullName ?? "");

            foreach (var group in groups)
            {
                yield return new DirectoryGroup(group.Key, group.ToList());
            }
        }
    }

    public async Task DeleteMissingTracks(MusicLibrary library)
    {
        var indexedFiles = _context.AudioFiles
            .Where(f => f.Library.Id == library.Id)
            .AsEnumerable();
        var missingFiles = indexedFiles.Where(f => !Path.Exists(f.FilePath)).Select(f => f.Id);

        var deletedTracks = await _context.Tracks
            .Where(f => missingFiles.Contains(f.AudioFile.Id))
            .ExecuteDeleteAsync();

        if (deletedTracks > 0)
            _logger.LogInformation("Deleted {Tracks} missing tracks", deletedTracks);

        await DeleteEmptyArtistsAndAlbums();
        await _context.AudioFiles.Where(f => missingFiles.Contains(f.Id)).ExecuteDeleteAsync();
    }

    private async Task DeleteEmptyArtistsAndAlbums()
    {
        var deletedArtists = await _context.ArtistsWithRoles
            .Where(a => !a.Tracks.Any())
            .ExecuteDeleteAsync();
        if (deletedArtists > 0)
            _logger.LogInformation("Deleted {DeletedArtists} artists with no tracks", deletedArtists);

        var emptyAlbumsArtwork = await _context.Albums
            .Include(t => t.Artworks)
            .Where(a => !a.Tracks.Any())
            .Select(a => a.Artworks)
            .SelectMany(x => x)
            .ToListAsync();

        foreach (var artwork in emptyAlbumsArtwork)
        {
            await _artworkService.DeleteArtwork(artwork);
        }

        var deletedAlbums = await _context.Albums.Where(a => !a.Tracks.Any()).ExecuteDeleteAsync();
        if (deletedAlbums > 0)
            _logger.LogInformation("Deleted {DeletedAlbums} albums with no tracks", deletedAlbums);
    }
}
```

**Benefits:**
- **Single Responsibility:** Only handles file system operations
- **Independently Testable:** Can test with mock file systems
- **Reusable:** FileSystemWatcher can use this to detect changes
- **Memory Efficient:** Streams directory groups, doesn't load entire library
- **Clean Separation:** No database entity creation, just file enumeration

### 4. Refactor IndexerService - Pure Indexing

**Purpose:** Index audio files to database (no file system scanning)

**New Signature:**

```csharp
public interface IIndexerService
{
    // Process a single directory group, stream indexed tracks
    IAsyncEnumerable<Track> IndexDirectory(DirectoryGroup directoryGroup, MusicLibrary library);

    // File system event handlers (still needed)
    Task DeleteTrack(string filePath);
    Task HandleRename(string oldPath, string newPath);
}
```

**Slim Dependencies:**

```csharp
public class IndexerService : IIndexerService
{
    private readonly CoralDbContext _context;
    private readonly ISearchService _searchService;  // KEPT - fast, synchronous keyword indexing
    private readonly IArtworkService _artworkService;
    private readonly ILogger<IndexerService> _logger;

    // Only 4 core dependencies (removed: IMapper, MusicLibraryRegisteredEventEmitter, IEmbeddingChannel)

    public IndexerService(
        CoralDbContext context,
        ISearchService searchService,
        IArtworkService artworkService,
        ILogger<IndexerService> logger)
    {
        _context = context;
        _searchService = searchService;
        _artworkService = artworkService;
        _logger = logger;
    }

    public async IAsyncEnumerable<Track> IndexDirectory(
        DirectoryGroup directoryGroup,
        MusicLibrary library)
    {
        var analyzedTracks = await ReadTracksInDirectory(directoryGroup.Files);
        bool folderIsAlbum = analyzedTracks.Select(x => x.Album).Distinct().Count() == 1;

        if (folderIsAlbum)
        {
            await foreach (var track in IndexAlbum(analyzedTracks, library))
            {
                yield return track;
            }
        }
        else
        {
            await foreach (var track in IndexSingleFiles(analyzedTracks, library))
            {
                yield return track;
            }
        }
    }

    private async IAsyncEnumerable<Track> IndexAlbum(List<ATL.Track> tracks, MusicLibrary library)
    {
        // Parse all artists
        var artistForTracks = new Dictionary<ATL.Track, List<ArtistWithRole>>();
        foreach (var track in tracks)
        {
            var artists = await ParseArtists(track.Artist, track.Title);
            artistForTracks.Add(track, artists);
        }

        // Get or create album
        var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();
        var albumType = AlbumTypeHelper.GetAlbumType(
            distinctArtists.Count(a => a.Role == ArtistRole.Main),
            tracks.Count());
        var indexedAlbum = await GetAlbum(distinctArtists, tracks.First());
        indexedAlbum.Type = albumType;

        // Get or create genres
        var distinctGenres = tracks.Where(t => t.Genre != null).Select(t => t.Genre).Distinct();
        var createdGenres = new List<Genre>();
        foreach (var genre in distinctGenres)
        {
            var indexedGenre = await GetGenre(genre);
            createdGenres.Add(indexedGenre);
        }

        // Index each track
        foreach (var trackToIndex in tracks)
        {
            var targetGenre = createdGenres.FirstOrDefault(g => g.Name == trackToIndex.Genre);
            var track = await IndexFile(
                artistForTracks[trackToIndex],
                indexedAlbum,
                targetGenre,
                trackToIndex,
                library);

            // Insert search keywords inline (fast, synchronous)
            await _searchService.InsertKeywordsForTrack(track);

            // Stream track to caller
            yield return track;
        }
    }

    private async IAsyncEnumerable<Track> IndexSingleFiles(List<ATL.Track> tracks, MusicLibrary library)
    {
        foreach (var atlTrack in tracks)
        {
            var artists = await ParseArtists(atlTrack.Artist, atlTrack.Title);
            var indexedAlbum = await GetAlbum(artists, atlTrack);
            var indexedGenre = await GetGenre(atlTrack.Genre);
            var track = await IndexFile(artists, indexedAlbum, indexedGenre, atlTrack, library);

            await _searchService.InsertKeywordsForTrack(track);
            yield return track;
        }
    }

    // ... existing helper methods (GetAlbum, GetGenre, GetArtist, etc.) ...
}
```

**What stays in IndexerService:**
- `ISearchService` - search keyword indexing (fast, synchronous)
- `IArtworkService` - artwork extraction/processing (inline with track)
- `CoralDbContext` - database access for creating entities

**What gets removed:**
- `IEmbeddingChannel` - moved to ScanWorker
- `IMapper` - moved to NewLibraryService (Phase 1)
- `MusicLibraryRegisteredEventEmitter` - replaced by IScanChannel
- File system scanning logic - moved to DirectoryScanner

### 3. Create LibraryService - Library Management

**Purpose:** Separate library CRUD from indexing logic

```csharp
public interface ILibraryService
{
    Task<List<MusicLibraryDto>> GetMusicLibraries();
    Task<MusicLibrary?> AddMusicLibrary(string path);
    Task RemoveMusicLibrary(Guid libraryId);
    Task<MusicLibrary?> GetMusicLibrary(Guid libraryId);
}

public class LibraryService : ILibraryService
{
    private readonly CoralDbContext _context;
    private readonly IMapper _mapper;
    private readonly IScanChannel _scanChannel;
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(
        CoralDbContext context,
        IMapper mapper,
        IScanChannel scanChannel,
        ILogger<LibraryService> logger)
    {
        _context = context;
        _mapper = mapper;
        _scanChannel = scanChannel;
        _logger = logger;
    }

    public async Task<MusicLibrary?> AddMusicLibrary(string path)
    {
        try
        {
            var contentDirectory = new DirectoryInfo(path);
            if (!contentDirectory.Exists)
            {
                throw new ApplicationException("Content directory does not exist.");
            }

            var library = await _context.MusicLibraries.FirstOrDefaultAsync(m => m.LibraryPath == path)
                          ?? new MusicLibrary()
                          {
                              LibraryPath = path,
                              AudioFiles = new List<AudioFile>()
                          };

            _context.MusicLibraries.Add(library);
            await _context.SaveChangesAsync();

            // Queue initial scan instead of emitting event
            var requestId = Guid.NewGuid().ToString();
            await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                Library: library,
                SpecificDirectory: null,
                Incremental: false,
                RequestId: requestId,
                Trigger: ScanTrigger.LibraryAdded
            ));

            _logger.LogInformation("Library added and scan queued: {Path} (RequestId: {RequestId})", path, requestId);

            return library;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to add music library {Path}", path);
            return null;
        }
    }

    public async Task<List<MusicLibraryDto>> GetMusicLibraries()
    {
        return await _context.MusicLibraries
            .ProjectTo<MusicLibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    // Additional methods...
}
```

### 5. Update EmbeddingChannel - Include RequestId

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

### 6. Create ScanWorker - Orchestration Layer

**Purpose:** Coordinate scanning, embedding queue, and progress reporting

```csharp
public class ScanWorker : BackgroundService
{
    private readonly IScanChannel _scanChannel;
    private readonly IEmbeddingChannel _embeddingChannel;
    private readonly IScanReporter _scanReporter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(
        IScanChannel scanChannel,
        IEmbeddingChannel embeddingChannel,
        IScanReporter scanReporter,
        IServiceScopeFactory scopeFactory,
        ILogger<ScanWorker> logger)
    {
        _scanChannel = scanChannel;
        _embeddingChannel = embeddingChannel;
        _scanReporter = scanReporter;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanWorker started!");

        await foreach (var job in _scanChannel.GetReader().ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessScan(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scan job for library {Library}", job.Library.LibraryPath);
            }
        }

        _logger.LogWarning("ScanWorker stopped!");
    }

    private async Task ProcessScan(ScanJob job, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var directoryScanner = scope.ServiceProvider.GetRequiredService<IDirectoryScanner>();
        var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();

        // Step 1: Delete missing tracks
        await directoryScanner.DeleteMissingTracks(job.Library);

        // Step 2: Count total files upfront
        var totalFiles = await directoryScanner.CountFiles(job.Library, job.Incremental);

        // Step 3: Register scan with expected track count
        _scanReporter.RegisterScan(job.RequestId, totalFiles, job.Library);

        var tracksIndexed = 0;
        var directoriesScanned = 0;

        // Step 4: Stream directory groups and process each one
        await foreach (var directoryGroup in directoryScanner.ScanLibrary(job.Library, job.Incremental))
        {
            try
            {
                // Index this directory group, stream tracks
                await foreach (var track in indexer.IndexDirectory(directoryGroup, job.Library))
                {
                    tracksIndexed++;

                    // Report indexing progress
                    _scanReporter.ReportTrackIndexed(job.RequestId);

                    // Queue for embedding with requestId
                    await _embeddingChannel.GetWriter().WriteAsync(
                        new EmbeddingJob(track, job.RequestId), ct);
                }

                directoriesScanned++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index directory {Directory}", directoryGroup.DirectoryPath);
            }

            // Periodic change tracker clear
            if (directoriesScanned % 25 == 0)
            {
                await using var clearScope = _scopeFactory.CreateAsyncScope();
                var context = clearScope.ServiceProvider.GetRequiredService<CoralDbContext>();
                context.ChangeTracker.Clear();
            }
        }

        // Step 5: Update library last scan time
        await using var finalScope = _scopeFactory.CreateAsyncScope();
        var finalContext = finalScope.ServiceProvider.GetRequiredService<CoralDbContext>();
        var library = await finalContext.MusicLibraries.FindAsync(job.Library.Id);
        if (library != null)
        {
            library.LastScan = DateTime.UtcNow;
            await finalContext.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Scan completed for {Library}: {Directories} directories, {Tracks} tracks indexed",
            job.Library.LibraryPath,
            directoriesScanned,
            tracksIndexed);
    }
}
```

**Key changes:**
- **Removed callback pattern** - Clean async streaming with `IAsyncEnumerable<Track>`
- **Uses DirectoryScanner** - Separates file system scanning from indexing
- **Counts files upfront** - Registers scan with accurate expected count
- **Streams directory groups** - Memory efficient, processes one directory at a time
- **Streams indexed tracks** - Each track immediately reported and queued for embeddings
- **Clean orchestration** - DirectoryScanner → IndexerService → ScanReporter → EmbeddingChannel

### 7. Update EmbeddingWorker - Report Progress

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

### 8. Refactor FileSystemWatcher to FileSystemWorker

**Purpose:** Improve reliability and decouple from IndexerService

```csharp
public class FileSystemWorker : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IScanChannel _scanChannel;
    private readonly ILogger<FileSystemWorker> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly MemoryCache _debounceCache;
    private readonly SemaphoreSlim _semaphore = new(1);
    private const int DebounceMilliseconds = 250;

    public FileSystemWorker(
        IServiceProvider serviceProvider,
        IScanChannel scanChannel,
        ILogger<FileSystemWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _scanChannel = scanChannel;
        _logger = logger;
        _debounceCache = new MemoryCache(new MemoryCacheOptions());
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        InitializeFileSystemWatchers();
        return Task.CompletedTask;
    }

    private void InitializeFileSystemWatchers()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();

        foreach (var musicLibrary in context.MusicLibraries.ToList())
        {
            var watcher = new FileSystemWatcher(musicLibrary.LibraryPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            watcher.Changed += (s, e) => HandleFileSystemEvent(e);
            watcher.Created += (s, e) => HandleFileSystemEvent(e);
            watcher.Renamed += (s, e) => HandleRenameEvent(e);
            watcher.Deleted += (s, e) => HandleDeleteEvent(e);

            _watchers.Add(watcher);
            _logger.LogInformation("Watching {Path} for changes...", musicLibrary.LibraryPath);
        }
    }

    private void HandleFileSystemEvent(FileSystemEventArgs e)
    {
        var parentDir = Directory.GetParent(e.FullPath)?.FullName;
        if (parentDir == null) return;

        // Debounce: reset timer on each event for the same directory
        _debounceCache.Set(parentDir, e, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(DebounceMilliseconds),
            PostEvictionCallbacks = { new PostEvictionCallbackRegistration
            {
                EvictionCallback = (key, value, reason, state) =>
                {
                    if (reason == EvictionReason.Expired && value is FileSystemEventArgs args)
                    {
                        _ = Task.Run(() => EnqueueDirectoryScan(args.FullPath));
                    }
                }
            }}
        });
    }

    private async Task EnqueueDirectoryScan(string path)
    {
        // Wait for file to be fully written
        if (File.Exists(path))
        {
            var isReady = await WaitForFileReady(path, CancellationToken.None);
            if (!isReady)
            {
                _logger.LogWarning("File not ready after waiting: {Path}", path);
                return;
            }
        }

        var parentDir = Directory.Exists(path) ? path : Directory.GetParent(path)?.FullName;
        if (parentDir == null) return;

        var library = GetMusicLibraryForPath(parentDir);
        if (library == null)
        {
            _logger.LogError("Unable to find music library for: {Path}", parentDir);
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                Library: library,
                SpecificDirectory: parentDir,
                Incremental: true,
                RequestId: null, // No progress reporting for automatic scans
                Trigger: ScanTrigger.FileSystemEvent
            ));

            _logger.LogInformation("Queued scan for directory: {Path}", parentDir);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<bool> WaitForFileReady(string path, CancellationToken ct)
    {
        const int maxAttempts = 10;
        const int delayMs = 500;
        long previousSize = -1;

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists) return false;

                // Check if size is stable
                if (fileInfo.Length == previousSize && previousSize > 0)
                {
                    // Try opening for exclusive read to verify no write lock
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }

                previousSize = fileInfo.Length;
                await Task.Delay(delayMs, ct);
            }
            catch (IOException)
            {
                // File still being written
                await Task.Delay(delayMs, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking file readiness: {Path}", path);
                return false;
            }
        }

        return false;
    }

    private void HandleRenameEvent(RenamedEventArgs args)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();

            try
            {
                await indexer.HandleRename(args.OldFullPath, args.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle rename from {Old} to {New}", args.OldFullPath, args.FullPath);
            }
        });
    }

    private void HandleDeleteEvent(FileSystemEventArgs args)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();

            try
            {
                await indexer.DeleteTrack(args.FullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle delete for {Path}", args.FullPath);
            }
        });
    }

    private MusicLibrary? GetMusicLibraryForPath(string path)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CoralDbContext>();
        return context.MusicLibraries.FirstOrDefault(l => path.StartsWith(l.LibraryPath));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }
        _debounceCache.Dispose();
        _semaphore.Dispose();
    }
}
```

### 6. Performance Optimizations - In-Memory Caching

**Goal:** Eliminate 99.99% of database queries during indexing by pre-loading reference data

#### Query Count Comparison

| Operation | Current | With Caching |
|-----------|---------|--------------|
| **Initial load** | 0 queries | **5 queries** (once at start) |
| **Genre lookups** | 7,000 queries | **0 queries** (in-memory) |
| **Artist lookups** | ~15,000 queries | **0 queries** (in-memory) |
| **Album lookups** | 7,000 queries | **0 queries** (in-memory) |
| **Track lookups** | 7,000 queries | **0 queries** (in-memory) |
| **Record label lookups** | ~500 queries | **0 queries** (in-memory) |
| **Artist saves** | 500 SaveChanges | **0 queries** (batched) |
| **TOTAL for 7k tracks** | **~37,000 queries** | **~6-10 queries** |
| **TOTAL for 50k tracks** | **~265,000 queries** | **~6-10 queries** |

#### Album Identification Strategy

**Challenge:** Albums need composite keys for proper deduplication.

**Current approach uses tuple:**
```csharp
var key = (albumName, atlTrack.Year, atlTrack.DiscTotal, atlTrack.TrackTotal);
```

**Problems:**
- Hard to read tuple syntax
- Only stores year, loses month/day information
- Should preserve full release dates when available

**Solution: Use a proper record type with full date support:**

```csharp
/// <summary>
/// Composite key for identifying unique albums.
/// Albums with the same name can be different releases (e.g., remaster vs original, different release dates).
/// </summary>
public record AlbumKey(
    string Name,
    DateOnly? ReleaseDate,
    int? DiscTotal,
    int? TrackTotal)
{
    /// <summary>
    /// Create from ATL track metadata.
    /// Note: ATL.Track.Date contains the full release date (if present in tags).
    /// Some users have full dates (YYYY-MM-DD), others only years (YYYY).
    /// We preserve the full date when available for accurate deduplication.
    /// </summary>
    public static AlbumKey FromTrack(ATL.Track track, string? fallbackName = null)
    {
        var albumName = !string.IsNullOrEmpty(track.Album)
            ? track.Album
            : fallbackName ?? Path.GetFileName(Path.GetDirectoryName(track.Path));

        // ATL.Track.Date is DateTime? with full date (YYYY-MM-DD)
        // ATL.Track.Year is int? with just year (for backwards compatibility)
        DateOnly? releaseDate = track.Date.HasValue
            ? DateOnly.FromDateTime(track.Date.Value)
            : (track.Year.HasValue ? new DateOnly(track.Year.Value, 1, 1) : null);

        return new AlbumKey(
            Name: albumName!,
            ReleaseDate: releaseDate,
            DiscTotal: track.DiscTotal,
            TrackTotal: track.TrackTotal
        );
    }
}
```

**Schema Changes Required:**

**1. Album Model - Full Release Date Support**

```csharp
// Album.cs - Update model
public class Album : BaseTable
{
    // ... existing properties ...

    [Obsolete("Use ReleaseDate instead. Kept for backwards compatibility.")]
    public int? ReleaseYear { get; set; } // Calculate from ReleaseDate.Year

    public DateOnly? ReleaseDate { get; set; } // New: stores full date (YYYY-MM-DD)
}
```

**Migration strategy:**
1. Add `ReleaseDate` column (nullable)
2. Migrate existing data: `UPDATE Albums SET ReleaseDate = MAKE_DATE(ReleaseYear, 1, 1) WHERE ReleaseYear IS NOT NULL`
3. Update indexer to populate `ReleaseDate` from `atlTrack.Date`
4. Keep `ReleaseYear` as computed property for backwards compatibility (calculate from `ReleaseDate.Year`)
5. Eventually deprecate `ReleaseYear` column

**2. Track Model - Preserve Original Artist String**

```csharp
// Track.cs - Update model
public class Track : BaseTable
{
    // ... existing properties ...

    public List<ArtistWithRole> Artists { get; set; } = null!; // Parsed artists

    public string? OriginalArtistString { get; set; } // NEW: Original tag value (e.g., "Daft Punk, Pharrell Williams")
}
```

**Why preserve the original artist string?**

The indexer parses and splits artist strings (e.g., `"A & B, C"` → `["A", "B", "C"]`) to create normalized artist entities. However, the **original tag format is often better for UI display**:

- Original: `"Daft Punk feat. Pharrell Williams"` ✅ Readable, preserves intent
- Reconstructed: `"Daft Punk, Pharrell Williams"` ❌ Lost formatting nuance

**Frontend benefits:**
```typescript
// Use original string when available, fall back to reconstructed
const displayArtist = track.originalArtistString ??
                      track.artists.filter(a => a.role === 'Main')
                                   .map(a => a.name)
                                   .join(', ');
```

**Migration strategy:**
1. Add `OriginalArtistString` column (nullable)
2. Existing tracks: Leave null (or populate from `Artists` collection if desired)
3. Update indexer to populate from `atlTrack.Artist` before parsing
4. Frontend prefers `OriginalArtistString` when available

**Benefits:**
- Accurate deduplication (same album, different release dates)
- Preserves full date information when available in tags
- Better user experience (shows actual release dates, not just years)
- Graceful fallback to year-only for older tags

#### Caching Implementation

```csharp
private class ScanContext
{
    // ===== Pre-loaded existing entities (read-only) =====
    public Dictionary<string, Genre> ExistingGenres { get; init; } = new();
    public Dictionary<string, Artist> ExistingArtists { get; init; } = new();
    public Dictionary<string, RecordLabel> ExistingLabels { get; init; } = new();
    public Dictionary<AlbumKey, Album> ExistingAlbums { get; init; } = new();
    public Dictionary<string, Track> ExistingTracks { get; init; } = new();

    // ===== Newly created entities during this scan =====
    public Dictionary<string, Genre> NewGenres { get; } = new();
    public Dictionary<string, Artist> NewArtists { get; } = new();
    public Dictionary<string, RecordLabel> NewLabels { get; } = new();
    public Dictionary<AlbumKey, Album> NewAlbums { get; } = new();

    // ===== Batch buffers for persistence =====
    public List<Genre> GenresToAdd { get; } = new();
    public List<Artist> ArtistsToAdd { get; } = new();
    public List<RecordLabel> LabelsToAdd { get; } = new();
    public List<Album> AlbumsToAdd { get; } = new();
    public List<Track> TracksToAdd { get; } = new();
    public List<Track> TracksToUpdate { get; } = new();
}

// Pre-load all reference data once at scan start
private async Task<ScanContext> PreloadReferenceData(MusicLibrary library)
{
    _logger.LogInformation("Pre-loading reference data for library {Library}...", library.LibraryPath);

    // Load ALL reference data in parallel (5 queries total)
    var genresTask = _context.Genres
        .AsNoTracking() // Read-only, faster
        .ToListAsync();

    var artistsTask = _context.Artists
        .AsNoTracking()
        .ToListAsync();

    var labelsTask = _context.RecordLabels
        .AsNoTracking()
        .ToListAsync();

    // Load only THIS library's albums to reduce memory footprint
    var albumsTask = _context.Albums
        .Include(a => a.Artists)
        .Where(a => a.Tracks.Any(t => t.AudioFile.Library.Id == library.Id))
        .ToListAsync(); // Need tracking for updates

    // Load only THIS library's tracks (for existence/update checks)
    var tracksTask = _context.Tracks
        .Include(t => t.AudioFile)
        .Where(t => t.AudioFile.Library.Id == library.Id)
        .ToListAsync(); // Need tracking for updates

    await Task.WhenAll(genresTask, artistsTask, labelsTask, albumsTask, tracksTask);

    var context = new ScanContext
    {
        ExistingGenres = genresTask.Result.ToDictionary(g => g.Name),
        ExistingArtists = artistsTask.Result.ToDictionary(a => a.Name),
        ExistingLabels = labelsTask.Result.ToDictionary(l => l.Name),
        ExistingTracks = tracksTask.Result.ToDictionary(t => t.AudioFile.FilePath)
    };

    // Build album dictionary using AlbumKey
    foreach (var album in albumsTask.Result)
    {
        var key = new AlbumKey(
            album.Name,
            album.ReleaseDate, // Full date now!
            album.DiscTotal,
            album.TrackTotal
        );
        context.ExistingAlbums[key] = album;
    }

    _logger.LogInformation(
        "Pre-loaded: {Genres} genres, {Artists} artists, {Albums} albums, {Tracks} tracks (library-specific)",
        context.ExistingGenres.Count,
        context.ExistingArtists.Count,
        context.ExistingAlbums.Count,
        context.ExistingTracks.Count
    );

    return context;
}

// Zero database queries during lookups!
private Genre GetOrCreateGenre(string name, ScanContext ctx)
{
    if (ctx.ExistingGenres.TryGetValue(name, out var existing))
        return existing;

    if (ctx.NewGenres.TryGetValue(name, out var created))
        return created;

    var genre = new Genre { Name = name };
    ctx.NewGenres[name] = genre;
    ctx.GenresToAdd.Add(genre);

    _logger.LogDebug("Creating new genre: {Name}", name);
    return genre;
}

private Artist GetOrCreateArtist(string name, ScanContext ctx)
{
    if (string.IsNullOrEmpty(name)) name = "Unknown Artist";

    if (ctx.ExistingArtists.TryGetValue(name, out var existing))
        return existing;

    if (ctx.NewArtists.TryGetValue(name, out var created))
        return created;

    var artist = new Artist { Name = name };
    ctx.NewArtists[name] = artist;
    ctx.ArtistsToAdd.Add(artist);

    _logger.LogDebug("Creating new artist: {Name}", name);
    return artist;
}

private Album GetOrCreateAlbum(
    List<ArtistWithRole> artists,
    ATL.Track atlTrack,
    ScanContext ctx)
{
    var key = AlbumKey.FromTrack(atlTrack);

    // Check existing albums
    if (ctx.ExistingAlbums.TryGetValue(key, out var existing))
    {
        // Ensure all artists are associated with this album
        var missingArtists = artists.Where(a =>
            !existing.Artists.Any(ea => ea.ArtistId == a.ArtistId));

        foreach (var artist in missingArtists)
        {
            existing.Artists.Add(artist);
        }

        return existing;
    }

    // Check newly created albums
    if (ctx.NewAlbums.TryGetValue(key, out var created))
        return created;

    // Create new album
    var label = GetOrCreateRecordLabel(atlTrack, ctx);
    var album = new Album
    {
        Name = key.Name,
        ReleaseDate = key.ReleaseDate,
        ReleaseYear = key.ReleaseDate?.Year, // Backwards compatibility
        DiscTotal = key.DiscTotal,
        TrackTotal = key.TrackTotal,
        CatalogNumber = atlTrack.CatalogNumber,
        Label = label,
        Artists = new List<ArtistWithRole>(artists),
        Artworks = new List<Artwork>(),
        CreatedAt = DateTime.UtcNow
    };

    ctx.NewAlbums[key] = album;
    ctx.AlbumsToAdd.Add(album);

    _logger.LogDebug("Creating new album: {Name} ({Date})", key.Name, key.ReleaseDate?.ToString("yyyy-MM-dd") ?? "no date");
    return album;
}

private Track AddOrUpdateTrack(
    List<ArtistWithRole> artists,
    Album album,
    Genre? genre,
    ATL.Track atlTrack,
    MusicLibrary library,
    ScanContext ctx)
{
    // Check if track already exists
    if (ctx.ExistingTracks.TryGetValue(atlTrack.Path, out var existing))
    {
        // Update existing track metadata
        existing.Album = album;
        existing.Artists = artists;
        existing.OriginalArtistString = atlTrack.Artist; // Preserve original tag value
        existing.Title = !string.IsNullOrEmpty(atlTrack.Title)
            ? atlTrack.Title
            : Path.GetFileName(atlTrack.Path);
        existing.Genre = genre;
        existing.Comment = atlTrack.Comment;
        existing.DiscNumber = atlTrack.DiscNumber;
        existing.TrackNumber = atlTrack.TrackNumber;
        existing.DurationInSeconds = atlTrack.Duration;
        existing.Isrc = atlTrack.ISRC;
        existing.UpdatedAt = DateTime.UtcNow;

        // Update audio file metadata
        existing.AudioFile.UpdatedAt = File.GetLastWriteTimeUtc(atlTrack.Path);
        existing.AudioFile.FileSizeInBytes = new FileInfo(atlTrack.Path).Length;
        existing.AudioFile.AudioMetadata = GetAudioMetadata(atlTrack);

        ctx.TracksToUpdate.Add(existing);
        _logger.LogDebug("Updating track: {Path}", atlTrack.Path);
        return existing;
    }

    // Create new track
    var track = new Track
    {
        Id = Guid.NewGuid(),
        Album = album,
        Artists = artists,
        OriginalArtistString = atlTrack.Artist, // Preserve original tag value
        Title = !string.IsNullOrEmpty(atlTrack.Title)
            ? atlTrack.Title
            : Path.GetFileName(atlTrack.Path),
        Genre = genre,
        Comment = atlTrack.Comment,
        DiscNumber = atlTrack.DiscNumber,
        TrackNumber = atlTrack.TrackNumber,
        DurationInSeconds = atlTrack.Duration,
        Isrc = atlTrack.ISRC,
        Keywords = new List<Keyword>(),
        AudioFile = new AudioFile
        {
            FilePath = atlTrack.Path,
            UpdatedAt = File.GetLastWriteTimeUtc(atlTrack.Path),
            FileSizeInBytes = new FileInfo(atlTrack.Path).Length,
            AudioMetadata = GetAudioMetadata(atlTrack),
            Library = library
        }
    };

    ctx.TracksToAdd.Add(track);
    _logger.LogDebug("Creating new track: {Path}", atlTrack.Path);
    return track;
}
```

#### Batch Persistence

```csharp
private async Task FlushBatch(ScanContext ctx)
{
    var sw = Stopwatch.StartNew();

    // Add entities in dependency order (foreign keys)
    if (ctx.GenresToAdd.Any())
        await _context.Genres.AddRangeAsync(ctx.GenresToAdd);

    if (ctx.ArtistsToAdd.Any())
        await _context.Artists.AddRangeAsync(ctx.ArtistsToAdd);

    if (ctx.LabelsToAdd.Any())
        await _context.RecordLabels.AddRangeAsync(ctx.LabelsToAdd);

    if (ctx.AlbumsToAdd.Any())
        await _context.Albums.AddRangeAsync(ctx.AlbumsToAdd);

    if (ctx.TracksToAdd.Any())
        await _context.Tracks.AddRangeAsync(ctx.TracksToAdd);

    if (ctx.TracksToUpdate.Any())
        _context.Tracks.UpdateRange(ctx.TracksToUpdate);

    // Single SaveChanges for entire batch
    await _context.SaveChangesAsync();

    _logger.LogInformation(
        "Flushed batch: +{NewTracks} tracks, ~{UpdatedTracks} tracks in {Ms}ms",
        ctx.TracksToAdd.Count,
        ctx.TracksToUpdate.Count,
        sw.ElapsedMilliseconds
    );

    // Clear batch buffers
    ctx.GenresToAdd.Clear();
    ctx.ArtistsToAdd.Clear();
    ctx.LabelsToAdd.Clear();
    ctx.AlbumsToAdd.Clear();
    ctx.TracksToAdd.Clear();
    ctx.TracksToUpdate.Clear();
}
```

**Memory Usage Estimate:**

For a 50,000 track library:
- Genres: 200 × 100 bytes = ~20 KB
- Artists: 5,000 × 200 bytes = ~1 MB
- Labels: 500 × 200 bytes = ~100 KB
- Albums: 10,000 × 500 bytes = ~5 MB
- Tracks: 50,000 × 1 KB = ~50 MB
- **Total: ~56 MB** (completely feasible)

**Performance Expectations:**

| Library Size | Current Time | Optimized Time | Query Count |
|-------------|-------------|----------------|-------------|
| 7,000 tracks | ~10 minutes | ~30-60 seconds | ~10 queries |
| 50,000 tracks | Hours? | ~5-10 minutes | ~10 queries |

## Benefits

### 1. Separation of Concerns

**IndexerService:**
- Pure transformation: file paths → database entities
- No side effects to channels, events, or external services
- 3 dependencies instead of 7
- Easy to test in isolation

**ScanWorker:**
- Orchestrates scan workflow
- Handles search indexing and embedding queue
- Reports progress via SignalR
- Natural place for error handling and retry logic

**LibraryService:**
- Manages library CRUD operations
- Handles DTO projections
- Owns library lifecycle

### 2. Better Testability

```csharp
[Fact]
public async Task ScanLibrary_ValidFiles_ReturnsIndexedTracks()
{
    // No need to mock channels, search service, event emitters, mappers
    var indexer = new IndexerService(_context, _artworkService, _logger);

    var result = await indexer.ScanLibrary(library);

    Assert.Equal(3, result.IndexedTracks.Count);
    Assert.Empty(result.Errors);
}
```

### 3. Progress Reporting

`IProgress<ScanProgress>` flows naturally:
- IndexerService reports internal progress
- ScanWorker forwards to SignalR
- No tight coupling to SignalR in business logic

### 4. Channel-Based Architecture

- Natural backpressure and queuing
- Can prioritize jobs (manual vs. automatic)
- Easy to add retry logic
- Better than event emitters for async workflows

### 5. Improved FileSystemWatcher

- Waits for file to be fully written (size stable + no write lock)
- Reduces retry loops in `ReadTracksInDirectory`
- Better error handling and logging

## Implementation Plan

### Phase 1: Foundation (No Breaking Changes)

**Goal:** Set up new infrastructure without breaking existing code

1. Create `IScanChannel`, `ScanChannel`, `ScanJob` types
2. Create `ILibraryService`, `LibraryService` (copy methods from IndexerService)
3. Register new services in DI container
4. Create basic `ScanWorker` that reads from channel (no-op for now)

**Test:** Verify new services register and ScanWorker starts

### Phase 2: Refactor IndexerService

**Goal:** Make IndexerService return data instead of side-effecting

1. Change `ScanLibrary` signature to return `ScanResult`
2. Change `ScanDirectory` signature to return `ScanResult`
3. Add `IProgress<ScanProgress>` parameter
4. Remove `_embeddingChannel` usage from `AddOrUpdateTrack`
5. Remove `_searchService` usage from `IndexFile`
6. Update internal methods to return `List<Track>`

**Test:** Update existing IndexerService tests to work with new signatures

### Phase 3: Implement ScanWorker Orchestration

**Goal:** Move orchestration logic to ScanWorker

1. Implement `ProcessScan` method in ScanWorker
2. Add search indexing loop
3. Add embedding channel writes
4. Add SignalR progress reporting
5. Add error handling

**Test:** End-to-end test of ScanWorker → IndexerService → SignalR

### Phase 4: Migrate LibraryService

**Goal:** Move library management out of IndexerService

1. Update controllers to use `ILibraryService` instead of `IIndexerService`
2. Remove `AddMusicLibrary`, `GetMusicLibraries` from `IIndexerService`
3. Remove `IMapper` dependency from IndexerService
4. Remove `MusicLibraryRegisteredEventEmitter` dependency

**Test:** Verify library CRUD operations work via new service

### Phase 5: Replace IndexerWorker

**Goal:** Modernize FileSystemWatcher implementation

1. Create `FileSystemWorker` with improved file-ready detection
2. Update to write to `IScanChannel` instead of calling IndexerService directly
3. Register FileSystemWorker in DI
4. Remove old `IndexerWorker`
5. Remove `MusicLibraryRegisteredEventEmitter` entirely

**Test:** Verify file system changes trigger scans correctly

### Phase 6: Fix Test Isolation

**Goal:** Speed up tests by avoiding separate Postgres containers

**Option A: Schema-based isolation**
```csharp
private TestDatabase CreateDatabase(string schemaName)
{
    return new TestDatabase(opt =>
    {
        opt.UseNpgsql(Container.GetConnectionString(), p =>
        {
            p.UseVector();
            p.MigrationsHistoryTable("__EFMigrationsHistory", schemaName);
        });
    }, schemaName);
}
```

**Option B: Transaction rollback**
```csharp
public class IndexerServiceTests : IAsyncLifetime
{
    private DbContextTransaction? _transaction;

    public async Task InitializeAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction != null)
            await _transaction.RollbackAsync();
    }
}
```

**Option C: Unique test data per test**
- Create test data factories instead of shared `TestDataRepository`
- Each test generates unique library path
- No overlap between tests

**Test:** Run all tests in parallel and verify no conflicts

### Phase 7: SignalR Integration

**Goal:** Expose scan operations via SignalR hub

1. Create `IndexerHub` with scan methods
2. Update `LibraryController` to trigger scans via `IScanChannel`
3. Add endpoint for on-demand directory rescans
4. Document SignalR events and message formats

**Test:** Frontend can trigger scans and receive progress updates

## Migration Strategy

### Backwards Compatibility

During transition, keep both patterns working:

```csharp
// Old pattern (deprecated)
public async Task ScanLibrary(MusicLibrary library, bool incremental = false)
{
    var result = await ScanLibraryWithProgress(library, incremental, null);
    // Maintain old behavior by doing side effects here if needed
}

// New pattern
public async Task<ScanResult> ScanLibraryWithProgress(
    MusicLibrary library,
    bool incremental = false,
    IProgress<ScanProgress>? progress = null)
{
    // New implementation
}
```

Once all callers migrate, remove old methods.

### Rollback Plan

Each phase is independent. If issues arise:
- Phase 1-2: New code not used yet, safe to revert
- Phase 3: Can keep old IndexerWorker alongside new ScanWorker
- Phase 4+: Feature flag to switch between old/new library service

## Testing Strategy

The new architecture makes testing **easier**, **faster**, and **more isolated**. Here's the comprehensive approach:

### Test Structure Changes

#### 1. IndexerService Unit Tests (Faster, Simpler)

**Old approach** (slow, many dependencies):
```csharp
private Services CreateServices()
{
    var testDatabase = CreateDatabase();
    var searchLogger = Substitute.For<ILogger<SearchService>>();
    var indexerLogger = Substitute.For<ILogger<IndexerService>>();
    var artworkLogger = Substitute.For<ILogger<ArtworkService>>();
    var paginationService = Substitute.For<IPaginationService>();
    var embeddingChannel = new EmbeddingChannel();
    var searchService = new SearchService(...);
    var artworkService = new ArtworkService(...);
    var eventEmitter = new MusicLibraryRegisteredEventEmitter();
    var indexerService = new IndexerService(
        testDatabase.Context, searchService, indexerLogger, artworkService,
        eventEmitter, testDatabase.Mapper, embeddingChannel);

    return new Services()
    {
        TestDatabase = testDatabase,
        IndexerService = indexerService,
    };
}

[Fact]
public async Task ReadDirectory_MixedAlbums_CreatesTwoAlbums()
{
    var services = CreateServices();
    await services.IndexerService.ScanLibrary(TestDataRepository.MixedAlbumTags);

    // Assert by querying database
    var jupiter = await services.TestDatabase.Context.ArtistsWithRoles
        .Include(a => a.Albums)
        .FirstAsync(a => a.Artist.Name == "Jupiter");

    Assert.Single(jupiter.Albums);
}
```

**New approach** (fast, minimal dependencies):
```csharp
private IndexerService CreateIndexerService(CoralDbContext context)
{
    var artworkLogger = Substitute.For<ILogger<ArtworkService>>();
    var indexerLogger = Substitute.For<ILogger<IndexerService>>();
    var artworkService = new ArtworkService(context, artworkLogger);

    // Only 3 dependencies!
    return new IndexerService(context, artworkService, indexerLogger);
}

[Fact]
public async Task ScanLibrary_MixedAlbums_ReturnsCorrectTracks()
{
    // Arrange
    var context = CreateTestContext();
    var indexer = CreateIndexerService(context);
    var library = TestDataRepository.MixedAlbumTags;

    // Act
    var result = await indexer.ScanLibrary(library);

    // Assert on return value (no database queries needed!)
    Assert.Equal(6, result.IndexedTracks.Count);
    Assert.Equal(2, result.DirectoriesScanned);
    Assert.Empty(result.Errors);

    // Verify tracks have correct albums
    var jupiterTracks = result.IndexedTracks.Where(t => t.Album.Name == "Moons");
    var neptuneTracks = result.IndexedTracks.Where(t => t.Album.Name == "Discovery");

    Assert.Equal(3, jupiterTracks.Count());
    Assert.Equal(3, neptuneTracks.Count());
}
```

**Benefits:**
- No need to mock channels, search service, event emitters, mapper
- Assert on return values instead of database state
- Faster execution (fewer dependencies to set up)
- Clearer test intent

#### 2. ScanWorker Integration Tests (Orchestration)

Test the full pipeline with mocked external dependencies:

```csharp
public class ScanWorkerTests : IAsyncLifetime
{
    private readonly IScanChannel _scanChannel;
    private readonly IEmbeddingChannel _embeddingChannel;
    private readonly IHubContext<IndexerHub> _mockHub;
    private readonly TestDatabase _testDatabase;
    private readonly ScanWorker _scanWorker;

    public ScanWorkerTests()
    {
        _scanChannel = new ScanChannel();
        _embeddingChannel = new EmbeddingChannel();
        _mockHub = Substitute.For<IHubContext<IndexerHub>>();
        _testDatabase = CreateTestDatabase();

        // Create real service provider with test services
        var serviceProvider = BuildServiceProvider();

        _scanWorker = new ScanWorker(
            _scanChannel,
            _embeddingChannel,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _mockHub,
            Substitute.For<ILogger<ScanWorker>>()
        );
    }

    [Fact]
    public async Task ProcessScan_FullLibrary_IndexesAndQueuesEmbeddings()
    {
        // Arrange
        var library = await CreateTestLibrary();
        var job = new ScanJob(library, null, false, "test-request-id");

        // Act - Write to channel and let worker process
        await _scanChannel.GetWriter().WriteAsync(job);
        await Task.Delay(1000); // Give worker time to process

        // Assert - Verify SignalR progress was reported
        await _mockHub.Clients.All.Received().SendAsync(
            "ScanProgress",
            Arg.Is<object>(o => o.GetType().GetProperty("Status")!.GetValue(o)!.ToString() == "Completed")
        );

        // Assert - Verify tracks queued for embeddings
        var embeddedTracks = new List<Track>();
        while (_embeddingChannel.GetReader().TryRead(out var track))
        {
            embeddedTracks.Add(track);
        }
        Assert.NotEmpty(embeddedTracks);
    }
}
```

#### 3. Test Isolation - Three Approaches

**Current problem:** Each test needs separate Postgres container (slow)

**Solution A: Schema-Based Isolation** (Recommended - fastest)

```csharp
public class IndexerServiceTests : IAsyncLifetime
{
    private readonly string _schemaName = $"test_{Guid.NewGuid():N}";
    private CoralDbContext _context = null!;

    public async Task InitializeAsync()
    {
        // Create schema for this test
        _context = CreateContextWithSchema(_schemaName);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        // Drop schema after test
        await _context.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {_schemaName} CASCADE");
        await _context.DisposeAsync();
    }

    private CoralDbContext CreateContextWithSchema(string schema)
    {
        var options = new DbContextOptionsBuilder<CoralDbContext>()
            .UseNpgsql(
                SharedPostgresContainer.ConnectionString,
                o => o.UseVector().MigrationsHistoryTable("__EFMigrationsHistory", schema)
            )
            .Options;

        var context = new CoralDbContext(options);
        context.Database.ExecuteSqlRaw($"CREATE SCHEMA IF NOT EXISTS {schema}");
        context.Database.ExecuteSqlRaw($"SET search_path TO {schema}");

        return context;
    }
}

// Shared container across all tests
public class SharedPostgresContainer
{
    private static readonly PostgreSqlContainer _container;
    public static string ConnectionString => _container.GetConnectionString();

    static SharedPostgresContainer()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:0.8.1-pg17-trixie")
            .Build();

        _container.StartAsync().Wait();
    }
}
```

**Benefits:**
- All tests share ONE Postgres container (fast startup)
- Tests run in parallel (isolated schemas)
- No test data conflicts
- ~10x faster than separate containers

**Solution B: Transaction Rollback** (Alternative - even faster)

```csharp
public class IndexerServiceTests : IAsyncLifetime
{
    private DbContextTransaction _transaction = null!;
    private CoralDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = CreateSharedContext();
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task ScanLibrary_ValidFiles_ReturnsIndexedTracks()
    {
        // Test runs in transaction
        // Automatically rolled back after test
    }
}
```

**Benefits:**
- Fastest approach (no schema creation overhead)
- Perfect isolation
- Automatic cleanup

**Trade-off:**
- Can't test transaction logic
- Nested transactions may be tricky

**Solution C: Unique Test Data** (Most realistic)

```csharp
public class TestDataFactory
{
    public static MusicLibrary CreateUniqueLibrary(CoralDbContext context)
    {
        var uniqueId = Guid.NewGuid().ToString();
        var testDir = Path.Combine(Path.GetTempPath(), "CoralTests", uniqueId);

        Directory.CreateDirectory(testDir);
        CopyTestFiles(testDir);

        var library = new MusicLibrary
        {
            LibraryPath = testDir,
            AudioFiles = new List<AudioFile>()
        };

        context.MusicLibraries.Add(library);
        return library;
    }
}

[Fact]
public async Task ScanLibrary_ValidFiles_ReturnsIndexedTracks()
{
    var library = TestDataFactory.CreateUniqueLibrary(_context);
    await _context.SaveChangesAsync();

    var result = await _indexer.ScanLibrary(library);

    Assert.NotEmpty(result.IndexedTracks);
}
```

**Benefits:**
- Most realistic (actual file system)
- No artificial isolation needed
- Tests real I/O behavior

**Trade-off:**
- Slower (file copies)
- More complex cleanup

#### 4. Performance Testing

Test that caching actually eliminates queries:

```csharp
public class IndexerPerformanceTests
{
    [Fact]
    public async Task ScanLibrary_7000Tracks_UsesMinimalQueries()
    {
        // Arrange
        var context = CreateInstrumentedContext(); // Logs all queries
        var indexer = new IndexerService(context, _artworkService, _logger);
        var library = CreateLibraryWith7000Tracks();

        // Act
        var queryCount = 0;
        context.Database.QueryExecuted += (_, __) => queryCount++;

        var sw = Stopwatch.StartNew();
        var result = await indexer.ScanLibrary(library);
        sw.Stop();

        // Assert
        Assert.InRange(queryCount, 5, 15); // Should be ~10 queries max
        Assert.InRange(sw.Elapsed.TotalSeconds, 0, 120); // Should be under 2 minutes
        Assert.Equal(7000, result.IndexedTracks.Count);
    }

    [Fact]
    public async Task PreloadReferenceData_LoadsAllDataInParallel()
    {
        // Arrange
        var context = CreateContextWithData();
        var indexer = new IndexerService(context, _artworkService, _logger);

        // Act
        var sw = Stopwatch.StartNew();
        var scanContext = await indexer.PreloadReferenceData(library);
        sw.Stop();

        // Assert
        Assert.NotEmpty(scanContext.ExistingGenres);
        Assert.NotEmpty(scanContext.ExistingArtists);
        Assert.InRange(sw.Elapsed.TotalMilliseconds, 0, 1000); // Fast parallel load
    }
}
```

#### 5. Album Deduplication Tests

Test that the new `AlbumKey` works correctly:

```csharp
public class AlbumKeyTests
{
    [Fact]
    public void AlbumKey_SameNameDifferentDates_AreDifferent()
    {
        var track1 = CreateTrack("Discovery", new DateTime(2001, 3, 12));
        var track2 = CreateTrack("Discovery", new DateTime(2011, 11, 21)); // Remaster

        var key1 = AlbumKey.FromTrack(track1);
        var key2 = AlbumKey.FromTrack(track2);

        Assert.NotEqual(key1, key2); // Different release dates = different albums
    }

    [Fact]
    public void AlbumKey_FullDateAndYearOnly_Match()
    {
        var trackWithFullDate = CreateTrack("Discovery", new DateTime(2001, 3, 12));
        var trackWithYearOnly = CreateTrack("Discovery", year: 2001);

        var key1 = AlbumKey.FromTrack(trackWithFullDate);
        var key2 = AlbumKey.FromTrack(trackWithYearOnly);

        // Year-only should normalize to Jan 1
        Assert.Equal(new DateOnly(2001, 1, 1), key2.ReleaseDate);
    }

    [Fact]
    public void AlbumKey_UsedAsDictionaryKey_WorksCorrectly()
    {
        var albums = new Dictionary<AlbumKey, Album>();
        var track = CreateTrack("Discovery", new DateTime(2001, 3, 12));
        var key = AlbumKey.FromTrack(track);

        albums[key] = new Album { Name = "Discovery" };

        // Create same key from different track instance
        var track2 = CreateTrack("Discovery", new DateTime(2001, 3, 12));
        var key2 = AlbumKey.FromTrack(track2);

        Assert.True(albums.ContainsKey(key2)); // Value equality works
    }
}
```

#### 6. Migration Tests

Test that existing data migrates correctly:

```csharp
public class AlbumMigrationTests
{
    [Fact]
    public async Task MigrateReleaseYear_ConvertsToReleaseDate()
    {
        // Arrange - Create old-style albums with ReleaseYear only
        var album = new Album
        {
            Name = "Test Album",
            ReleaseYear = 2020,
            ReleaseDate = null // Old data
        };
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Act - Run migration
        await RunMigration("ConvertReleaseYearToDate");

        // Assert
        var migrated = await _context.Albums.FindAsync(album.Id);
        Assert.NotNull(migrated.ReleaseDate);
        Assert.Equal(new DateOnly(2020, 1, 1), migrated.ReleaseDate);
        Assert.Equal(2020, migrated.ReleaseYear); // Still populated for backwards compat
    }
}
```

### Test Organization

```
tests/
├── Coral.Services.Tests/
│   ├── IndexerService/
│   │   ├── IndexerServiceTests.cs          # Core indexing logic
│   │   ├── AlbumKeyTests.cs                # Album deduplication
│   │   ├── CachingTests.cs                 # Pre-loading and lookups
│   │   └── PerformanceTests.cs             # Query count verification
│   ├── ScanWorker/
│   │   ├── ScanWorkerTests.cs              # Orchestration tests
│   │   └── ProgressReportingTests.cs       # SignalR integration
│   ├── LibraryService/
│   │   └── LibraryServiceTests.cs          # Library CRUD
│   └── Helpers/
│       ├── TestDataFactory.cs              # Unique test data generation
│       └── SharedPostgresContainer.cs      # Container reuse
└── Coral.Integration.Tests/
    ├── FullPipelineTests.cs                # End-to-end scenarios
    └── MigrationTests.cs                   # Data migration verification
```

### Test Execution Speed Comparison

| Approach | Current | Schema Isolation | Transaction Rollback |
|----------|---------|------------------|---------------------|
| **Container startup** | 10s × 15 tests = 150s | 10s × 1 container = 10s | 10s × 1 container = 10s |
| **Per-test overhead** | ~5s (new DB) | ~0.5s (schema create) | ~0.01s (transaction) |
| **15 tests total** | ~225s (3m 45s) | ~17.5s | ~10.5s |
| **Speedup** | Baseline | **~13x faster** | **~21x faster** |

### Recommended Approach

**Use Schema Isolation for most tests:**
- Good balance of speed and realism
- Tests run in parallel
- Easy to debug (can inspect schemas)

**Use Transaction Rollback for unit tests:**
- Fastest for pure logic tests
- Perfect for IndexerService unit tests

**Use Integration tests sparingly:**
- Full pipeline tests with real dependencies
- Verify SignalR, channels, workers interact correctly
- Run these in CI, not locally

## Additional Indexing Improvements

These fixes address current album classification issues that should be integrated into the refactored indexer:

### 1. Fix Compilation Detection Logic

**Problem:** Albums with a recurring main artist + different featured artists on each track are incorrectly marked as compilations.

**Example scenario:**
```
Album: "Producer Showcase"
Track 1: Producer A & Vocalist B (main artists)
Track 2: Producer A & Vocalist C (main artists)
Track 3: Producer A & Vocalist D (main artists)
Track 4: Producer A & Vocalist E (main artists)
Track 5: Producer A & Vocalist F (main artists)
```

**Current behavior (src/Coral.Services/IndexerService.cs:393-398):**
```csharp
var distinctArtists = artistForTracks.Values.SelectMany(a => a).DistinctBy(a => a.Artist.Id).ToList();

var albumType = AlbumTypeHelper.GetAlbumType(
    distinctArtists.Count(a => a.Role == ArtistRole.Main),
    tracks.Count()
);
```

This counts 6 distinct main artists (A, B, C, D, E, F), which exceeds the threshold of 4, triggering compilation detection.

**Root cause:** The logic counts ALL distinct artists across the entire album, when it should detect if there's a *consistent* core artist across all tracks. The current approach doesn't distinguish between:
- **True compilation:** Different artists on every track with no recurring pattern
- **Featured album:** One main artist + rotating guests (common in hip-hop, electronic, etc.)

**Proposed solution:**

Update AlbumTypeHelper to check for artist consistency:

```csharp
public static class AlbumTypeHelper
{
    public static AlbumType GetAlbumType(int mainArtistCount, int trackCount, Dictionary<ATL.Track, List<ArtistWithRole>> trackArtists)
    {
        var maxTracksForSingle = 2;
        var maxTracksForEP = 4;
        var maxTracksForMiniAlbum = 9;

        // Check if this is a compilation by analyzing artist distribution
        if (IsCompilation(trackArtists))
        {
            return AlbumType.Compilation;
        }

        // ... existing track count logic ...
    }

    private static bool IsCompilation(Dictionary<ATL.Track, List<ArtistWithRole>> trackArtists)
    {
        // Get main artists for each track
        var tracksWithMainArtists = trackArtists
            .Select(kvp => kvp.Value.Where(a => a.Role == ArtistRole.Main).Select(a => a.Artist.Id).ToList())
            .ToList();

        // Check if there's at least one artist that appears on ALL tracks
        var allArtists = tracksWithMainArtists.SelectMany(a => a).Distinct().ToList();
        var hasRecurringArtist = allArtists.Any(artistId =>
            tracksWithMainArtists.All(trackArtists => trackArtists.Contains(artistId))
        );

        if (hasRecurringArtist)
        {
            // There's a consistent artist - not a compilation
            return false;
        }

        // No recurring artist AND more than 4 distinct artists = likely compilation
        var totalDistinctArtists = allArtists.Count;
        return totalDistinctArtists > 4;
    }
}
```

**Alternative (simpler) approach:**

Check if any artist appears on > 50% of tracks:

```csharp
private static bool IsCompilation(Dictionary<ATL.Track, List<ArtistWithRole>> trackArtists)
{
    var trackCount = trackArtists.Count;
    var mainArtistAppearances = new Dictionary<Guid, int>();

    // Count appearances per artist
    foreach (var (track, artists) in trackArtists)
    {
        foreach (var artist in artists.Where(a => a.Role == ArtistRole.Main))
        {
            mainArtistAppearances.TryGetValue(artist.Artist.Id, out var count);
            mainArtistAppearances[artist.Artist.Id] = count + 1;
        }
    }

    // If ANY artist appears on >50% of tracks, it's not a compilation
    var maxAppearances = mainArtistAppearances.Values.Max();
    if (maxAppearances > trackCount / 2)
    {
        return false;
    }

    // Otherwise, check distinct artist count
    return mainArtistAppearances.Count > 4;
}
```

**Recommendation:** Use the second approach (50% threshold). It's more flexible and handles edge cases like split artist albums where 2 artists alternate tracks.

### 2. Album Type Override API

**Problem:** Users cannot manually correct album types when automatic detection is wrong.

**Proposed solution:**

Add endpoint to LibraryController:

```csharp
[HttpPatch]
[Route("albums/{albumId}/type")]
public async Task<ActionResult> UpdateAlbumType(Guid albumId, [FromBody] UpdateAlbumTypeRequest request)
{
    try
    {
        await _libraryService.UpdateAlbumType(albumId, request.AlbumType);
        return Ok();
    }
    catch (ArgumentException ex)
    {
        return NotFound(new { Message = ex.Message });
    }
}

public record UpdateAlbumTypeRequest(AlbumType AlbumType);
```

Add service method to LibraryService:

```csharp
public async Task UpdateAlbumType(Guid albumId, AlbumType albumType)
{
    var album = await _context.Albums.FindAsync(albumId);
    if (album == null)
    {
        throw new ArgumentException($"Album {albumId} not found.");
    }

    album.Type = albumType;
    album.UpdatedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
}
```

**Implementation notes:**
- Add in Phase 4 (LibraryService creation)
- Consider adding a "ManualOverride" flag to Album model to prevent re-indexing from overwriting user changes

### 3. Various Artists Compilation Detection

**Problem:** Albums with album artist "Various Artists" / "Various" / "VA" should be marked as compilations.

**Proposed solution:**

Check album artist tag before classification:

```csharp
private async Task IndexAlbum(List<ATL.Track> tracks, MusicLibrary library)
{
    var firstTrack = tracks.First();
    var albumArtist = firstTrack.AlbumArtist ?? firstTrack.Artist;

    // Check for Various Artists patterns
    var variousArtistNames = new[] { "Various Artists", "Various Artist", "Various", "VA", "V.A.", "V/A" };
    var isVariousArtists = variousArtistNames.Contains(albumArtist, StringComparer.OrdinalIgnoreCase);

    // ... existing parsing logic ...

    AlbumType albumType;
    if (isVariousArtists)
    {
        albumType = AlbumType.Compilation;
    }
    else
    {
        albumType = AlbumTypeHelper.GetAlbumType(distinctArtists.Count(a => a.Role == ArtistRole.Main), tracks.Count(), artistForTracks);
    }

    indexedAlbum.Type = albumType;
}
```

**Implementation notes:**
- Implement in Phase 2 when refactoring IndexerService
- Should this create an actual "Various Artists" artist entity, or just use it as a classification signal?

## Open Questions

1. **Channel capacity**: Should `IScanChannel` be bounded? What capacity?
   - Unbounded: Risk of memory growth if scans queue up
   - Bounded: Need backpressure strategy when full

2. **Scan prioritization**: Should manual scans preempt automatic FileSystemWatcher scans?
   - Could use priority queue
   - Or separate channels for manual vs. automatic

3. **Concurrent scans**: Should we allow multiple libraries to scan in parallel?
   - Current semaphore is sequential
   - Could parallelize per-library but serialize per-directory

4. **Progress granularity**: How often should we report progress?
   - Every directory? (could be chatty)
   - Every N directories?
   - Time-based throttling?

5. **Error handling**: What should happen when a scan partially fails?
   - Continue scanning remaining directories?
   - Abort entire scan?
   - Configurable retry policy?

6. **Test isolation**: Which approach for test speedup?
   - Schema-based isolation (cleanest)
   - Transaction rollback (fastest)
   - Unique test data (most realistic)

## Success Criteria

- [ ] IndexerService has ≤3 dependencies
- [ ] ScanWorker reports progress via SignalR
- [ ] FileSystemWatcher waits for files to be fully written
- [ ] Tests run without separate Postgres containers (or faster alternative)
- [ ] Can trigger directory rescan via API
- [ ] All existing tests pass with refactored code
- [ ] No regression in indexing accuracy or performance
