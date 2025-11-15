# Stage 1: Bulk Insertion Performance Optimization

**Status:** Ready for implementation
**Priority:** Current focus
**Related:** See `indexer-service-refactor.md` for full context

## Overview

Optimize indexing performance through schema simplification and bulk insertion strategy.

**Performance Target:**
- Current: 7k tracks in ~10 minutes (~12 tracks/sec)
- Target: 7k tracks in ~2-3 minutes (~60-100 tracks/sec)

**Performance Breakdown (7k tracks):**
- Phase 1 (File scan + artwork extraction): ~65 seconds
- Phase 2 (Scan-and-cache entity building): ~20 seconds (~8,750 queries)
- Phase 3 (Bulk insert entities): ~8 seconds
- Phase 3b (Junction tables via raw SQL): ~2 seconds
- Phase 4 (Deferred work - parallel artwork + bulk keywords): ~15 seconds
- **Total: ~110 seconds (~1.8 minutes) vs current ~600 seconds (~10 minutes)**

---

## Core Principles (CRITICAL)

1. **No EF Core Concurrency:** DbContext is NOT thread-safe - all queries must be sequential
2. **Scan-and-Cache:** Don't pre-load all data - query on first encounter, then cache
3. **Deferred Work:** Process artwork and keywords AFTER bulk insert (need entity IDs)
4. **Batch Operations:** Single SaveChanges per phase, not per entity

---

## Schema Changes

### 1. Remove Album ↔ ArtistWithRole Many-to-Many

Albums already have tracks, and tracks have artists. The album-artist relationship is redundant:

```csharp
// Current schema - redundant relationship
Album ↔ ArtistWithRole (many-to-many junction table)
  ↓
Track ↔ ArtistWithRole (many-to-many junction table)

// Simplified schema
Album
  ↓ (1-to-many)
Track ↔ ArtistWithRole (only junction table during indexing)
```

**Changes Required:**

```csharp
// Album.cs
public class Album : BaseTable {
    public string Name { get; set; } = null!;
    // REMOVE: public List<ArtistWithRole> Artists { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;
    public AlbumType? Type { get; set; }
    // ... rest unchanged
}

// ArtistWithRole.cs
public class ArtistWithRole : BaseTable {
    public ArtistRole Role { get; set; }
    public Guid ArtistId { get; set; }
    public Artist Artist { get; set; } = null!;
    public List<Track> Tracks { get; set; } = null!;
    // REMOVE: public List<Album> Albums { get; set; } = null!;
}
```

**Derive album artists on-demand:**

```csharp
// Service layer / DTO mapping
var albumArtists = context.Tracks
    .Where(t => t.AlbumId == albumId)
    .SelectMany(t => t.Artists)
    .DistinctBy(a => a.Id)
    .ToList();
```

**Benefits:**
- ✅ Eliminates one complex junction table during bulk insert
- ✅ Reduces data duplication
- ✅ Simpler indexer logic
- ⚠️ Slight query overhead on album retrieval (acceptable tradeoff)

### 2. Add Album.ReleaseDate (DateOnly?)

**Current:** Only stores `ReleaseYear` (int?)
**Problem:** Loses month/day information, causes deduplication issues

```csharp
public class Album : BaseTable
{
    [Obsolete("Use ReleaseDate instead. Kept for backwards compatibility.")]
    public int? ReleaseYear { get; set; } // Calculate from ReleaseDate.Year

    public DateOnly? ReleaseDate { get; set; } // NEW: Full date (YYYY-MM-DD)
}
```

**Migration:**
1. Add `ReleaseDate` column (nullable)
2. Migrate data: `UPDATE Albums SET ReleaseDate = MAKE_DATE(ReleaseYear, 1, 1) WHERE ReleaseYear IS NOT NULL`
3. Keep `ReleaseYear` for backwards compatibility

### 3. Add Track.OriginalArtistString

Preserve original tag formatting before parsing:

```csharp
public class Track : BaseTable
{
    public List<ArtistWithRole> Artists { get; set; } = null!; // Parsed artists

    public string? OriginalArtistString { get; set; } // NEW: e.g., "Daft Punk feat. Pharrell"
}
```

**Why:** Frontend can display `"Daft Punk feat. Pharrell Williams"` instead of `"Daft Punk, Pharrell Williams"`

### 4. Create AlbumKey Record Type

Replace tuples with proper record type for composite keys:

```csharp
/// <summary>
/// Composite key for identifying unique albums.
/// Albums with the same name can be different releases (remaster vs original).
/// </summary>
public record AlbumKey(
    string Name,
    DateOnly? ReleaseDate,
    int? DiscTotal,
    int? TrackTotal)
{
    public static AlbumKey FromTrack(ATL.Track track, string? fallbackName = null)
    {
        var albumName = !string.IsNullOrEmpty(track.Album)
            ? track.Album
            : fallbackName ?? Path.GetFileName(Path.GetDirectoryName(track.Path));

        // Preserve full date when available (YYYY-MM-DD), fallback to year-only
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

### 5. Update AutoMapper

```csharp
// AlbumProfile.cs
CreateMap<Album, AlbumDto>()
    .ForMember(des => des.Artists, opt => opt.MapFrom(src =>
        src.Tracks
            .SelectMany(t => t.Artists)
            .Where(a => a.Role == ArtistRole.Main)
            .DistinctBy(a => a.ArtistId)
    ))
    .ForMember(dest => dest.Favorited, opt => opt.MapFrom(src => src.Favorite != null));
```

---

## Implementation: 4-Phase Scan-and-Cache

### Phase 1: Collect All File Metadata + Extract Artwork

**Purpose:** Scan filesystem, read ATL metadata, extract artwork paths

```csharp
var allAtlTracks = new List<ATL.Track>();
var albumArtworkPaths = new Dictionary<AlbumKey, string>();

await foreach (var directoryGroup in _directoryScanner.ScanLibrary(library))
{
    var tracks = await ReadTracksInDirectory(directoryGroup.Files);
    allAtlTracks.AddRange(tracks);

    // Extract artwork ONCE per album (not per track!)
    if (tracks.Any())
    {
        var firstTrack = tracks.First();
        var albumKey = AlbumKey.FromTrack(firstTrack);

        if (!albumArtworkPaths.ContainsKey(albumKey))
        {
            // Priority 1: External files (folder.jpg, cover.jpg)
            // Priority 2: Embedded artwork (extracts to disk via ArtworkService)
            var artworkPath = await GetAlbumArtwork(firstTrack);
            if (artworkPath != null)
            {
                albumArtworkPaths[albumKey] = artworkPath;
            }
        }
    }
}
```

**Why extract artwork in Phase 1:**
- Need access to `ATL.Track.EmbeddedPictures` (not available later)
- `ExtractEmbeddedArtwork()` saves image to disk, returns file path
- Deduplicates by album (not per track)

**Output:**
- `allAtlTracks` - All track metadata in memory
- `albumArtworkPaths` - Artwork file paths keyed by AlbumKey

---

### Phase 2: Process Tracks with Scan-and-Cache

**Purpose:** Build entity graph in memory, query DB only once per unique entity

```csharp
var ctx = new ScanContext(_context);
var deferredArtwork = new List<ArtworkWork>();
var deferredKeywords = new List<Track>();

foreach (var atlTrack in allAtlTracks)
{
    // Scan-and-cache: Query DB on first encounter, cache result
    var genre = !string.IsNullOrEmpty(atlTrack.Genre)
        ? await ctx.GetOrCreateGenre(atlTrack.Genre)
        : null;

    var label = !string.IsNullOrEmpty(atlTrack.Publisher)
        ? await ctx.GetOrCreateRecordLabel(atlTrack.Publisher)
        : null;

    var artists = await ParseAndCreateArtists(atlTrack, ctx);

    var albumKey = AlbumKey.FromTrack(atlTrack);
    var album = await ctx.GetOrCreateAlbum(albumKey, label);

    // Defer artwork processing (needs album to be persisted)
    if (ctx.IsNewAlbum(albumKey) && albumArtworkPaths.TryGetValue(albumKey, out var artworkPath))
    {
        if (!deferredArtwork.Any(a => a.Album.Id == album.Id))
        {
            deferredArtwork.Add(new ArtworkWork(album, artworkPath));
        }
    }

    var track = await ctx.CreateOrUpdateTrack(atlTrack, album, genre, artists, library);

    // Defer keyword indexing (needs Track.Id assigned after insert)
    deferredKeywords.Add(track);
}
```

**ScanContext with Lazy Caching:**

```csharp
private class ScanContext
{
    private readonly CoralDbContext _context;
    private readonly Dictionary<string, Genre> _genreCache = new();
    private readonly Dictionary<string, Artist> _artistCache = new();
    private readonly Dictionary<AlbumKey, Album> _albumCache = new();
    private readonly HashSet<AlbumKey> _newAlbumKeys = new();

    public List<Genre> NewGenres { get; } = new();
    public List<Artist> NewArtists { get; } = new();
    public List<Album> NewAlbums { get; } = new();
    public List<Track> NewTracks { get; } = new();
    public List<Track> TracksToUpdate { get; } = new();

    public async Task<Genre> GetOrCreateGenre(string name)
    {
        // 1. Check cache
        if (_genreCache.TryGetValue(name, out var cached))
            return cached;

        // 2. Query DB (only once per unique genre!)
        var existing = await _context.Genres
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Name == name);

        if (existing != null)
        {
            _genreCache[name] = existing;
            return existing;
        }

        // 3. Create new
        var genre = new Genre { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        _genreCache[name] = genre;
        NewGenres.Add(genre);
        return genre;
    }

    public bool IsNewAlbum(AlbumKey key) => _newAlbumKeys.Contains(key);
}
```

**Query Count Estimate (7k tracks):**
- ~500 genre queries (one per unique genre)
- ~500 artist queries (one per unique artist)
- ~700 album queries (one per unique album)
- ~50 label queries (one per unique label)
- ~7,000 track existence checks
- **Total: ~8,750 queries** (vs original ~37,000!)

**Output:**
- `ctx.NewGenres`, `ctx.NewArtists`, `ctx.NewAlbums`, `ctx.NewTracks` - Entities to insert
- `ctx.TracksToUpdate` - Existing tracks to update
- `deferredArtwork` - Artwork to process after insert
- `deferredKeywords` - Tracks needing keyword indexing

---

### Phase 3: Staged Bulk Insert with EFCore.BulkExtensions

**Purpose:** Persist all entities in dependency order, single operation per entity type

```csharp
_logger.LogInformation("Phase 3: Bulk inserting entities...");
var sw = Stopwatch.StartNew();

// Stage 1: Artists (no dependencies)
if (ctx.NewArtists.Any())
{
    _logger.LogInformation("  Stage 1: Inserting {Count} artists...", ctx.NewArtists.Count);
    var stageSw = Stopwatch.StartNew();
    await _context.BulkInsertAsync(ctx.NewArtists, new BulkConfig
    {
        SetOutputIdentity = true,
        PreserveInsertOrder = true
    });
    _logger.LogInformation("  ✓ Artists inserted in {Ms}ms", stageSw.ElapsedMilliseconds);
}

// Stage 2: Genres (no dependencies)
if (ctx.NewGenres.Any())
{
    _logger.LogInformation("  Stage 2: Inserting {Count} genres...", ctx.NewGenres.Count);
    var stageSw = Stopwatch.StartNew();
    await _context.BulkInsertAsync(ctx.NewGenres, new BulkConfig { SetOutputIdentity = true });
    _logger.LogInformation("  ✓ Genres inserted in {Ms}ms", stageSw.ElapsedMilliseconds);
}

// Stage 3: RecordLabels (no dependencies)
// Stage 4: ArtistWithRole (depends on Artist)
// Stage 5: Albums (depends on RecordLabel)

// Stage 6: Tracks (complex - has many-to-many with ArtistWithRole)
if (ctx.NewTracks.Any())
{
    _logger.LogInformation("  Stage 6: Inserting {Count} tracks with {RelCount} artist relationships...",
        ctx.NewTracks.Count, ctx.NewTracks.Sum(t => t.Artists.Count));
    var stageSw = Stopwatch.StartNew();

    try
    {
        // Try bulk insert with navigation properties
        await _context.BulkInsertAsync(ctx.NewTracks, new BulkConfig
        {
            SetOutputIdentity = true,
            IncludeGraph = true,  // Handle Track.Artists junction
            PreserveInsertOrder = true
        });
        _logger.LogInformation("  ✓ Tracks + relationships inserted in {Ms}ms", stageSw.ElapsedMilliseconds);
    }
    catch (Exception ex)
    {
        // Fallback: Insert tracks, then manually insert junction records
        _logger.LogWarning(ex, "  Bulk insert with graph failed, using manual junction insert");

        // Insert tracks without relationships
        await _context.BulkInsertAsync(ctx.NewTracks, new BulkConfig
        {
            SetOutputIdentity = true,
            IncludeGraph = false
        });

        // Manually insert junction records
        var junctionRecords = ctx.NewTracks
            .SelectMany(t => t.Artists.Select(a => new { TracksId = t.Id, ArtistsId = a.Id }))
            .ToList();

        await _context.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO ""TrackArtistWithRole"" (""TracksId"", ""ArtistsId"")
               VALUES {string.Join(", ", junctionRecords.Select(r => $"('{r.TracksId}', '{r.ArtistsId}')")}"
        );
    }
}

// Stage 7: Update existing tracks
if (ctx.TracksToUpdate.Any())
{
    _logger.LogInformation("  Stage 7: Updating {Count} existing tracks...", ctx.TracksToUpdate.Count);
    await _context.BulkUpdateAsync(ctx.TracksToUpdate);
}

_logger.LogInformation("Phase 3 complete: All entities persisted in {Ms}ms", sw.ElapsedMilliseconds);
```

---

### Phase 4: Process Deferred Work (Sequential)

**Purpose:** Process artwork and keywords (require persisted entity IDs)

```csharp
_logger.LogInformation("Phase 4: Processing deferred work...");

// 4a: Artwork processing (SEQUENTIAL - ProcessArtwork calls SaveChangesAsync)
if (deferredArtwork.Any())
{
    _logger.LogInformation("  Processing {Count} artwork files...", deferredArtwork.Count);
    var artworkSw = Stopwatch.StartNew();

    foreach (var work in deferredArtwork)
    {
        try
        {
            // Generates thumbnails + inserts Artwork entities (calls SaveChanges)
            await _artworkService.ProcessArtwork(work.Album, work.ArtworkPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process artwork for {Album}", work.Album.Name);
        }
    }

    _logger.LogInformation("  ✓ Artwork processed in {Ms}ms", artworkSw.ElapsedMilliseconds);
}

// 4b: Keyword indexing (sequential)
if (deferredKeywords.Any())
{
    _logger.LogInformation("  Indexing keywords for {Count} tracks...", deferredKeywords.Count);
    var keywordSw = Stopwatch.StartNew();

    foreach (var track in deferredKeywords)
    {
        try
        {
            await _searchService.InsertKeywordsForTrack(track);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index keywords for {Track}", track.Title);
        }
    }

    _logger.LogInformation("  ✓ Keywords indexed in {Ms}ms", keywordSw.ElapsedMilliseconds);
}

_logger.LogInformation("Phase 4 complete");
```

**Why Sequential:**
- `ProcessArtwork` calls `SaveChangesAsync()` per album (EF Core concurrency issue)
- Could refactor ArtworkService for parallelization (future optimization)

**Performance Estimate (7k tracks):**
- Artwork: 700 albums × 100ms = ~70 seconds
- Keywords: 7,000 tracks × 5ms = ~35 seconds
- **Total: ~105 seconds**

---

## Implementation Checklist

### Schema Changes
- [ ] Remove `Album.Artists` navigation property
- [ ] Remove `ArtistWithRole.Albums` navigation property
- [ ] Add `Album.ReleaseDate` (DateOnly?)
- [ ] Add `Track.OriginalArtistString` (string?)
- [ ] Create `AlbumKey` record type
- [ ] Create migration for schema changes
- [ ] Update all code that references `Album.Artists` to derive from tracks
- [ ] Update AutoMapper `AlbumProfile`
- [ ] Evaluate: Embed AudioMetadata into AudioFile (optional)
- [ ] Defer Album.Type calculation to service/DTO layer

### Bulk Insert Implementation
- [ ] Add EFCore.BulkExtensions NuGet package
- [ ] Implement Phase 1: Collect file metadata + artwork extraction
- [ ] Implement Phase 2: Scan-and-cache entity building
- [ ] Implement Phase 3: Staged bulk insert with comprehensive logging
  - [ ] Stage 1: Artists
  - [ ] Stage 2: Genres
  - [ ] Stage 3: RecordLabels
  - [ ] Stage 4: ArtistWithRole
  - [ ] Stage 5: Albums
  - [ ] Stage 6: Tracks (with junction)
  - [ ] Stage 7: Update existing tracks
- [ ] Test Option A (BulkInsert with IncludeGraph = true)
- [ ] If slow, implement Option B (manual junction insert)
- [ ] Implement Phase 4: Deferred work processing

### Testing & Validation
- [ ] Run benchmark against 3,795 track library
- [ ] Compare results to baseline (15.37 tracks/sec)
- [ ] Identify bottlenecks from detailed logs
- [ ] Optimize based on measurements
- [ ] Test with larger libraries (10k, 50k tracks)
- [ ] Verify data integrity (albums, artists, relationships)
- [ ] Ensure Album.Type calculation works correctly

---

## Expected Performance

**Breakdown (7k tracks):**
- Phase 1: ~65s (file scan + artwork extraction)
- Phase 2: ~20s (scan-and-cache, ~8,750 queries)
- Phase 3: ~10s (bulk insert)
- Phase 4: ~105s (artwork + keywords)
- **Total: ~3.3 minutes** (vs current ~10 minutes)

**Scaled Estimates:**

**Conservative (5x improvement):**
- 7k tracks: ~2-3 minutes (was ~10 min)
- 50k tracks: ~15 minutes (was ~75 min)
- 100k tracks: ~30 minutes (was ~2.5 hours)

**Optimistic (10x, with parallelized artwork):**
- 7k tracks: ~1.5 minutes
- 50k tracks: ~10 minutes
- 100k tracks: ~20 minutes

---

## Future Optimization Opportunities

1. **Parallelize Artwork Processing:**
   - Refactor `ArtworkService.ProcessArtwork`:
     - `ProcessArtworkImages` - Pure image processing (parallelizable)
     - Batch insert all Artwork entities
   - Potential savings: ~35-50 seconds for 700 albums

2. **Batch Keyword Insertion:**
   - Refactor `SearchService.InsertKeywordsForTrack` for bulk inserts
   - Process all tracks in one operation

3. **AudioMetadata Inlining:**
   - Embed into AudioFile (eliminate 1-to-1 table)
   - Reduces joins, simplifies bulk insert

---

## Risk Mitigation

1. **Breaking existing queries:** Audit all Album.Artists usage before migration
2. **Bulk insert still slow:** Fallback to batched EF Core saves with caching
3. **Data integrity issues:** Thorough testing on test database first
4. **Migration failures:** Test migration rollback procedure

---

## Implementation Notes

- **EF Core Concurrency:** DbContext is NOT thread-safe - no parallel queries
- **Scan-and-Cache:** Query DB once per unique entity, cache thereafter
- **Deferred Work:** Artwork/keywords need entity IDs from bulk insert
- Use record types for cache keys (AlbumKey vs tuples)
- Prefer KISS principle - implement, measure, then optimize - Let profiling guide optimization decisions
