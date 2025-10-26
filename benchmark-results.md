# Indexer Performance Benchmarks

## Test Environment

- **Database:** PostgreSQL (coral3)
- **Test Library:** `C:\Benchmark Library`
- **Track Count:** 3,795 tracks
- **Hardware:** Windows PC
- **Build:** Release mode

---

## Baseline - Current Implementation (NewIndexerService)

**Date:** 2025-10-22

### Results

```
Total time:       246.99 seconds (~4.1 minutes)
Tracks indexed:   3,795
Speed:            15.37 tracks/sec
Avg per track:    65.08 ms
```

### Resource Usage

```
CPU:              1-5% (mostly idle)
Memory:           ~200 MB
```

### Performance Analysis

- **Bottleneck:** N+1 query problem
- **CPU is idle** 95-99% of the time waiting for database responses
- Individual `SaveChangesAsync()` calls for each artist creation
- Separate queries for each genre, artist, album, track lookup
- No caching of reference data
- EF Core change tracker overhead

### Extrapolated Performance

At this rate:
- **7,000 tracks:**  ~7.6 minutes
- **50,000 tracks:** ~54 minutes
- **100,000 tracks:** ~1.8 hours

---

## Optimization Target

### Planned Changes

1. **Query-first + cache pattern** for high-reuse entities (genres, artists, labels)
2. **EFCore.BulkExtensions** for bulk inserts (bypasses change tracker)
3. **Zero change tracker usage** (AsNoTracking on all queries)
4. **Batch flush** every 100 tracks instead of per-artist saves

### Expected Results

```
Speed:            75-150 tracks/sec (5-10x improvement)
CPU:              10-30% (actually working vs. waiting)
Memory:           200-300 MB (small cache overhead)
```

---

## Benchmark Command

```bash
cd C:\Projects\Coral\src\Coral.Cli
dotnet run --configuration Release -- "C:\Benchmark Library"
```

---

## Optimization 1 - Bulk Keywords & Artwork

**Date:** 2025-10-26

### Changes Implemented

1. **Bulk Keyword Insertion** (`SearchService.InsertKeywordsForTracksBulk`)
   - Extracts all unique keywords from tracks in a single pass
   - Creates new keywords using bulk extension pattern
   - Inserts track-keyword relationships using PostgreSQL `UNNEST` (chunks of 5000)
   - Reduces database round-trips from O(n × keywords) to O(1) batch operations

2. **Bulk Artwork Processing** (`ArtworkService.ProcessArtworksBulk`)
   - Processes images (original + 2 thumbnails per album) in memory first
   - Batches all artwork entities and inserts using `AddRangeAsync`
   - Maintains error handling per-album to prevent cascading failures

3. **Explicit Foreign Key IDs**
   - Set foreign key IDs explicitly on entities for better bulk insert compatibility
   - Ensures proper relationships without relying on navigation property resolution

### Results

```
Total time:       155.95 seconds (~2.6 minutes)
Tracks indexed:   3,795
Speed:            24.33 tracks/sec
Avg per track:    41.09 ms
```

### Performance Improvement vs Baseline

| Metric | Baseline | Current | Improvement |
|--------|----------|---------|-------------|
| **Total Time** | 246.99s | 155.95s | **-91.04s (36.9% faster)** |
| **Tracks/Second** | 15.37 | 24.33 | **+8.96 tracks/sec (58.3% faster)** |
| **Time per Track** | 65.08 ms | 41.09 ms | **-23.99 ms (36.9% faster)** |

### Extrapolated Performance

At the new rate of **24.33 tracks/sec**:
- **7,000 tracks:**  ~4.8 minutes (vs. 7.6 minutes baseline)
- **50,000 tracks:** ~34 minutes (vs. 54 minutes baseline)
- **100,000 tracks:** ~1.1 hours (vs. 1.8 hours baseline)

### Analysis

- **1.58x speedup** achieved through bulk operations
- Keyword and artwork operations no longer block indexing flow
- Still bottlenecked by individual track processing - next optimization target
- CPU utilization remains low, suggesting more room for improvement

---

## Optimization 2 - Parallelized Artwork Processing

**Date:** 2025-10-26

### Changes Implemented

1. **Parallel Image Processing** (`ArtworkService.ProcessArtworksParallel`)
   - Uses bounded parallelism with `SemaphoreSlim` limited to `Environment.ProcessorCount`
   - Processes multiple albums' images concurrently (thumbnails, color extraction)
   - CPU-intensive operations now utilize all available cores
   - Returns entity list without database operations

2. **Integrated into Bulk Pipeline**
   - Artwork processing moved **before** `SaveBulkChangesAsync`
   - Artwork entities added to BulkContext sequentially (thread-safe)
   - All entities (tracks, albums, artists, artworks) saved in one bulk operation
   - Eliminated unnecessary album reload from database

3. **BulkContext Array Support**
   - Added `string[]` type mapping for `Artwork.Colors` property
   - Uses `NpgsqlDbType.Array | NpgsqlDbType.Text` for proper PostgreSQL array handling

### Results

```
Total time:       136.89 seconds (~2.3 minutes)
Tracks indexed:   3,795
Speed:            27.72 tracks/sec
Avg per track:    36.07 ms
```

### Performance Improvement vs Previous

| Metric | Previous | Current | Improvement |
|--------|----------|---------|-------------|
| **Total Time** | 155.95s | 136.89s | **-19.06s (12.2% faster)** |
| **Tracks/Second** | 24.33 | 27.72 | **+3.39 tracks/sec (13.9% faster)** |
| **Time per Track** | 41.09 ms | 36.07 ms | **-5.02 ms (12.2% faster)** |

### Performance Improvement vs Baseline

| Metric | Baseline | Current | Total Improvement |
|--------|----------|---------|-------------------|
| **Total Time** | 246.99s | 136.89s | **-110.1s (44.6% faster)** |
| **Tracks/Second** | 15.37 | 27.72 | **+12.35 tracks/sec (80.4% faster)** |
| **Time per Track** | 65.08 ms | 36.07 ms | **-29.01 ms (44.6% faster)** |

**Overall speedup: 1.8x vs baseline**

### Extrapolated Performance

At the new rate of **27.72 tracks/sec**:
- **7,000 tracks:**  ~4.2 minutes (vs. 7.6 minutes baseline, 44% faster)
- **50,000 tracks:** ~30 minutes (vs. 54 minutes baseline, 44% faster)
- **100,000 tracks:** ~1.0 hours (vs. 1.8 hours baseline, 44% faster)

### Analysis

- **1.8x cumulative speedup** from baseline through bulk operations and parallelization
- Parallel image processing effectively utilizes CPU cores during artwork generation
- Eliminated database reload overhead by using AlbumId directly
- Artwork processing no longer sequential bottleneck
- Further improvements possible in track indexing phase

---
