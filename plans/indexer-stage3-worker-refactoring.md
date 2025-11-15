# Stage 3: Worker Architecture Refactoring

**Status:** Deferred until Stage 1 complete
**Priority:** Future enhancement
**Dependencies:** Stage 1 bulk insertion, optionally Stage 2 SignalR progress

## Overview

Improve architecture through better separation of concerns, improved FileSystemWatcher reliability, and cleaner worker orchestration.

**Goals:**
- Split IndexerService into focused components
- Separate file system scanning from entity indexing
- Improve FileSystemWatcher reliability with proper file-ready detection
- Better separation between scanning, indexing, and orchestration
- Make components independently testable

**Components:**
- **DirectoryScanner** - File system scanning only
- **IndexerService** - Pure transformation (files → entities)
- **LibraryService** - Library CRUD operations
- **ScanWorker** - Orchestration (coordinates scanner → indexer → channels)
- **FileSystemWorker** - Improved file watching with file-ready detection

---

## Service Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│ API Layer                                                    │
│  - LibraryController → ILibraryService                      │
│  - IndexerHub (SignalR) - if Stage 2 implemented            │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Orchestration Layer (Workers)                               │
│  - ScanWorker: Coordinates DirectoryScanner + IndexerService│
│  - FileSystemWorker: Watches files, triggers scans          │
│  - EmbeddingWorker: Processes embeddings (unchanged)        │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│ Business Logic Layer (Services)                             │
│  - IDirectoryScanner: File system scanning                  │
│  - IIndexerService: Entity creation (files → DB entities)   │
│  - ILibraryService: Library CRUD operations                 │
│  - ISearchService: Keyword indexing (unchanged)             │
│  - IArtworkService: Artwork processing (unchanged)          │
└─────────────────────────────────────────────────────────────┘
```

---

## Implementation Components

### 1. DirectoryScanner - File System Scanning

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

**Registration:**
```csharp
// Program.cs
services.AddScoped<IDirectoryScanner, DirectoryScanner>();
```

---

### 2. Refactor IndexerService - Pure Indexing

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
- `IMapper` - moved to LibraryService
- `MusicLibraryRegisteredEventEmitter` - replaced by IScanChannel (Stage 2)
- File system scanning logic - moved to DirectoryScanner

---

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
    private readonly IScanChannel _scanChannel;  // If Stage 2 implemented
    private readonly ILogger<LibraryService> _logger;

    public LibraryService(
        CoralDbContext context,
        IMapper mapper,
        IScanChannel scanChannel,  // Optional: only if Stage 2 implemented
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

            // If Stage 2 implemented: Queue initial scan
            if (_scanChannel != null)
            {
                var requestId = Guid.NewGuid().ToString();
                await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                    Library: library,
                    SpecificDirectory: null,
                    Incremental: false,
                    RequestId: requestId,
                    Trigger: ScanTrigger.LibraryAdded
                ));

                _logger.LogInformation("Library added and scan queued: {Path} (RequestId: {RequestId})", path, requestId);
            }
            else
            {
                // Fallback: Trigger scan directly (Stage 1 approach)
                _logger.LogInformation("Library added: {Path}", path);
            }

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

    public async Task RemoveMusicLibrary(Guid libraryId)
    {
        var library = await _context.MusicLibraries.FindAsync(libraryId);
        if (library != null)
        {
            _context.MusicLibraries.Remove(library);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Removed library {LibraryId}", libraryId);
        }
    }

    public async Task<MusicLibrary?> GetMusicLibrary(Guid libraryId)
    {
        return await _context.MusicLibraries.FindAsync(libraryId);
    }
}
```

**Registration:**
```csharp
// Program.cs
services.AddScoped<ILibraryService, LibraryService>();
```

---

### 4. Create ScanWorker - Orchestration Layer

**Purpose:** Coordinate scanning, indexing, embedding queue, and optionally progress reporting

**Note:** This assumes Stage 2 (SignalR progress) has been implemented. If not, remove `IScanReporter` and `IScanChannel` dependencies.

```csharp
public class ScanWorker : BackgroundService
{
    private readonly IScanChannel _scanChannel;
    private readonly IEmbeddingChannel _embeddingChannel;
    private readonly IScanReporter _scanReporter;  // Optional: only if Stage 2 implemented
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanWorker> _logger;

    public ScanWorker(
        IScanChannel scanChannel,
        IEmbeddingChannel embeddingChannel,
        IScanReporter scanReporter,  // Optional
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

        // Step 3: Register scan with expected track count (if Stage 2 implemented)
        _scanReporter?.RegisterScan(job.RequestId, totalFiles, job.Library);

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

                    // Report indexing progress (if Stage 2 implemented)
                    _scanReporter?.ReportTrackIndexed(job.RequestId);

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

**Registration:**
```csharp
// Program.cs
services.AddHostedService<ScanWorker>();
```

---

### 5. FileSystemWorker - Improved File Watching

**Purpose:** Improve reliability and decouple from IndexerService

**Key Improvements:**
- Proper file-ready detection (waits for file to be fully written)
- Debouncing with MemoryCache
- Writes to IScanChannel instead of calling IndexerService directly
- Better error handling

```csharp
public class FileSystemWorker : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IScanChannel _scanChannel;  // Optional: only if Stage 2 implemented
    private readonly ILogger<FileSystemWorker> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly MemoryCache _debounceCache;
    private readonly SemaphoreSlim _semaphore = new(1);
    private const int DebounceMilliseconds = 250;

    public FileSystemWorker(
        IServiceProvider serviceProvider,
        IScanChannel scanChannel,  // Optional
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
            if (_scanChannel != null)
            {
                // Stage 2 approach: Queue scan via channel
                await _scanChannel.GetWriter().WriteAsync(new ScanJob(
                    Library: library,
                    SpecificDirectory: parentDir,
                    Incremental: true,
                    RequestId: null, // No progress reporting for automatic scans
                    Trigger: ScanTrigger.FileSystemEvent
                ));

                _logger.LogInformation("Queued scan for directory: {Path}", parentDir);
            }
            else
            {
                // Stage 1 fallback: Call indexer directly
                _logger.LogInformation("Detected file change in: {Path}", parentDir);
                // ... trigger scan directly ...
            }
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

**Key improvements:**
- `WaitForFileReady()` ensures file is fully written before scanning
- Checks file size stability and attempts exclusive read lock
- Debouncing prevents duplicate scans for rapid file changes
- Clean separation: watches files, queues scans, doesn't index directly

**Registration:**
```csharp
// Program.cs
services.AddHostedService<FileSystemWorker>();
```

---

## Implementation Checklist

- [ ] Create `IDirectoryScanner`, `DirectoryScanner`, `DirectoryGroup`
- [ ] Refactor `IndexerService` to use `IAsyncEnumerable<Track>` and accept `DirectoryGroup`
- [ ] Create `ILibraryService`, `LibraryService`
- [ ] Create `ScanWorker` for orchestration
- [ ] Create `FileSystemWorker` with file-ready detection
- [ ] Register all services in DI container
- [ ] Update API controllers to use `ILibraryService`
- [ ] Remove old `IndexerWorker` if present
- [ ] Test file watching with large file copies
- [ ] Test incremental scans
- [ ] Test rename/delete operations

---

## Benefits

1. **Single Responsibility:** Each component has one clear purpose
2. **Testability:** DirectoryScanner can be tested with mock file systems
3. **Reusability:** DirectoryScanner used by both manual scans and FileSystemWatcher
4. **Reliability:** Proper file-ready detection prevents indexing partial files
5. **Memory Efficiency:** Streaming architecture prevents loading entire library
6. **Clean Architecture:** Clear separation between scanning, indexing, orchestration

---

## Migration Strategy

1. **Phase 1:** Create DirectoryScanner (doesn't break existing code)
2. **Phase 2:** Create LibraryService (move library CRUD from controllers)
3. **Phase 3:** Refactor IndexerService to use DirectoryScanner
4. **Phase 4:** Create ScanWorker if Stage 2 implemented, or integrate with existing worker
5. **Phase 5:** Replace FileSystemWatcher with FileSystemWorker
6. **Phase 6:** Remove old coupling once new architecture is validated

Can be implemented incrementally without breaking existing functionality.
