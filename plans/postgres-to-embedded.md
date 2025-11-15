# Database Migration Plan: PostgreSQL → SQLite + DuckDB

**Status**: Planning
**Target**: Migrate Coral from PostgreSQL to embedded databases
**Approach**: Complete replacement (no abstraction layer)
**Strategy**: SQLite first → validate with tests → then add DuckDB embeddings

---

## Executive Summary

This document outlines the migration from PostgreSQL to a dual-database embedded architecture:
- **SQLite**: Main transactional database (music library metadata, users, playlists)
- **DuckDB**: Analytical database for vector embeddings and similarity search

### Why This Architecture?

| Database | Use Case | Strength |
|----------|----------|----------|
| **SQLite** | OLTP - tracks, albums, artists, playlists | Fast point lookups, excellent write performance for transactional workloads, zero deployment friction |
| **DuckDB** | OLAP - 1280-dim embeddings, vector similarity | Columnar storage, VSS extension with HNSW indexing, 30-50x faster analytical queries |

### Key Benefits

- ✅ **Zero external dependencies** - no PostgreSQL server to install/manage
- ✅ **Single-file databases** - trivial backup/restore
- ✅ **Better performance fit** - each DB optimized for its workload
- ✅ **Simplified deployment** - works out of the box for end users
- ✅ **Incremental migration** - SQLite validated first, then DuckDB added
- ✅ **Test-driven** - tests validate each phase before moving forward

---

## Current Architecture Analysis

### PostgreSQL Components

**1. Main Tables (→ SQLite)**
```
Tracks, Albums, Artists, Genres, Playlists
AudioFiles, AudioMetadata, MusicLibraries
FavoriteTracks, FavoriteAlbums, FavoriteArtists
Keywords (for search), RecordLabels
```

**2. TrackEmbedding (→ DuckDB)**
```csharp
// Current: Postgres with pgvector
public class TrackEmbedding : BaseTable
{
    public Guid TrackId { get; set; }
    [Column(TypeName = "vector(1280)")]
    public Vector Embedding { get; set; } = null!;
}

// HNSW index configured:
// m=16, ef_construction=64, cosine similarity
```

**3. Bulk Insert System (Coral.BulkExtensions)**
- Uses `Npgsql.NpgsqlConnection.BeginBinaryImportAsync()`
- PostgreSQL COPY command with BINARY format
- UNNEST for junction table inserts
- **→ Needs SQLite port**

**4. Embedding Workflow**
```
IndexerService → EmbeddingWorker → InferenceService (Coral.Essentia.Cli)
                     ↓
              PostgreSQL TrackEmbeddings table (pgvector)
```

**5. Recommendation Query (LibraryService.cs:176-210)**
```csharp
// Uses pgvector cosine distance
.Select(t => new {
    Entity = t,
    Distance = t.Embedding.CosineDistance(trackEmbeddings.Embedding)
})
.OrderBy(t => t.Distance)
```

---

## Target Architecture

```
┌─────────────────────────────────────────────────────────┐
│                     Coral Application                    │
└──────────────┬─────────────────────┬────────────────────┘
               │                     │
               ▼                     ▼
   ┌──────────────────┐   ┌───────────────────────┐
   │  SQLite          │   │  DuckDB               │
   │  coral.db        │   │  embeddings.db        │
   │                  │   │                       │
   │  [OLTP]          │   │  [OLAP]               │
   │  - Tracks        │   │  - track_embeddings   │
   │  - Albums        │   │    * track_id         │
   │  - Artists       │   │    * embedding[1280]  │
   │  - Playlists     │   │  - HNSW index         │
   │  - Users         │   │    (cosine distance)  │
   │  - Keywords      │   │                       │
   └──────────────────┘   └───────────────────────┘
         │                          │
         └──────────┬───────────────┘
                    │
                    ▼
              Join by TrackId
         (recommendations workflow)
```

---

## Migration Phases

### Migration Strategy & Execution Order

**Approach**: Complete replacement (not provider abstraction)
- Remove ALL Postgres code immediately
- No support for running both providers
- Simpler codebase, clearer intent

**Execution Order**:
```
Phase 1: Setup EF Core (SQLite)
   ├─ Remove Npgsql packages
   ├─ Add SQLite packages
   ├─ Update CoralDbContext
   └─ Remove TrackEmbedding from EF

Phase 2: Re-create Migrations
   ├─ Delete all existing migrations
   └─ Create InitialSqlite migration

Phase 3: Setup Testing Infrastructure
   ├─ Remove Testcontainers packages
   ├─ Replace DatabaseFixture with in-memory SQLite
   └─ Delete SharedPostgresContainer

Phase 4: Get Tests Running (Expect Failures)
   ├─ Run: dotnet test
   └─ Expect: BulkExtensions failures (Npgsql missing)

Phase 5: Rewrite BulkExtensions for SQLite
   ├─ Remove ALL Postgres code
   ├─ Implement SQLite bulk insert
   └─ Implement SQLite junction inserts

Phase 6: Validate All Tests Pass
   └─ Run: dotnet test (should be green)

===== SQLite Migration Complete =====

Phase 7: Introduce DuckDB Dependency
   ├─ Add DuckDB.NET.Data.Full package
   └─ Add DuckDbEmbeddingsPath config

Phase 8: Create DuckDB Embedding Service
   ├─ Create IDuckDbEmbeddingService
   ├─ Implement VSS extension loading
   └─ Implement vector similarity queries

Phase 9: Rewrite EmbeddingWorker for DuckDB
   ├─ Remove TrackEmbedding EF usage
   └─ Write embeddings to DuckDB

Phase 10: Update LibraryService Recommendations
   └─ Query DuckDB instead of EF TrackEmbeddings

Phase 11: Test End-to-End Indexing Workflow
   ├─ Index small library
   ├─ Verify embeddings in DuckDB
   └─ Test recommendations API

===== Complete Migration Done =====
```

**Why This Order?**
1. ✅ **Validate SQLite first** - ensure core database works before touching embeddings
2. ✅ **Tests catch regressions** - bulk extensions failures are expected and guide fixes
3. ✅ **Clear milestones** - "all tests pass" is a concrete checkpoint
4. ✅ **Embeddings separate** - can be developed/tested independently after SQLite is stable
5. ✅ **Rollback friendly** - can stop at Phase 6 and have a working (non-embedding) system

---

### Phase 1: Setup EF Core (SQLite)

**Goal**: Replace PostgreSQL with SQLite in EF Core

**1.1 Update NuGet Packages**

File: `src/Coral.Database/Coral.Database.csproj`

**Remove:**
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
<PackageReference Include="Pgvector" Version="0.2.2" />
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.2" />
```

**Add:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
```

**Note**: DuckDB comes later (Phase 7)

**1.2 Update ApplicationConfiguration**

File: `src/Coral.Configuration/ApplicationConfiguration.cs`

Add SQLite database path:
```csharp
public static string SqliteDbPath => Path.Combine(DataDirectory, "coral.db");
```

**Note**: DuckDB path comes later (Phase 7)

---

### Phase 2: Re-create Migrations

**Goal**: Clean slate with SQLite migrations

**2.1 Update CoralDbContext**

File: `src/Coral.Database/CoralDbContext.cs`

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    if (!options.IsConfigured)
    {
        // OLD: options.UseNpgsql(...)
        // NEW:
        options.UseSqlite($"Data Source={ApplicationConfiguration.SqliteDbPath}");
    }
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // REMOVE ALL Postgres-specific code:
    // - modelBuilder.HasPostgresExtension("vector");
    // - TrackEmbedding HNSW index configuration

    // Leave empty or add SQLite-specific configurations if needed
}
```

**2.2 Remove TrackEmbedding from CoralDbContext**

File: `src/Coral.Database/CoralDbContext.cs`

```csharp
// DELETE this entire line:
// public DbSet<TrackEmbedding> TrackEmbeddings { get; set; } = null!;
```

**Note**: TrackEmbedding model file (`src/Coral.Database.Models/TrackEmbedding.cs`) stays for now - we'll delete it in Phase 9 when we rewrite EmbeddingWorker

**2.3 Delete All Existing Migrations**

```bash
# Delete PostgreSQL migrations directory
rm -rf src/Coral.Database/Migrations
```

**2.4 Create Fresh SQLite Migration**

```bash
# Create new InitialSqlite migration
dotnet ef migrations add InitialSqlite --project src/Coral.Database

# Verify migration was created
ls src/Coral.Database/Migrations/
```

The migration will include all tables **EXCEPT** TrackEmbedding (since we removed it from DbContext)

---

### Phase 3: Setup Testing Infrastructure

**Goal**: Replace Testcontainers with in-memory SQLite

**3.1 Update Coral.TestProviders.csproj**

File: `src/Coral.Services/DuckDbEmbeddingService.cs`

```csharp
using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;

namespace Coral.Services;

public interface IDuckDbEmbeddingService
{
    Task InitializeAsync();
    Task InsertEmbeddingAsync(Guid trackId, float[] embedding);
    Task<bool> HasEmbeddingAsync(Guid trackId);
    Task<List<(Guid TrackId, double Distance)>> GetSimilarTracksAsync(
        Guid trackId, int limit = 100, double maxDistance = 0.2);
}

public class DuckDbEmbeddingService : IDuckDbEmbeddingService
{
    private readonly string _connectionString;
    private readonly ILogger<DuckDbEmbeddingService> _logger;

    public DuckDbEmbeddingService(ILogger<DuckDbEmbeddingService> logger)
    {
        _connectionString = $"Data Source={ApplicationConfiguration.DuckDbEmbeddingsPath}";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // Install and load VSS extension
        _logger.LogInformation("Loading DuckDB VSS extension...");
        command.CommandText = "INSTALL vss; LOAD vss;";
        await command.ExecuteNonQueryAsync();

        // Create embeddings table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS track_embeddings (
                track_id VARCHAR PRIMARY KEY,
                embedding FLOAT[1280],
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )";
        await command.ExecuteNonQueryAsync();

        // Check if we have data
        command.CommandText = "SELECT COUNT(*) FROM track_embeddings";
        var count = (long)await command.ExecuteScalarAsync();

        _logger.LogInformation("DuckDB initialized with {Count} embeddings", count);

        // Create HNSW index if we have enough data (>1000 rows recommended)
        if (count > 1000)
        {
            _logger.LogInformation("Creating HNSW index on {Count} embeddings...", count);
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS track_embeddings_hnsw_idx
                ON track_embeddings
                USING HNSW(embedding)
                WITH (metric = 'cosine')";
            await command.ExecuteNonQueryAsync();
            _logger.LogInformation("HNSW index created successfully");
        }
        else if (count > 0)
        {
            _logger.LogInformation(
                "Skipping HNSW index creation (need >1000 rows, have {Count})", count);
        }
    }

    public async Task InsertEmbeddingAsync(Guid trackId, float[] embedding)
    {
        if (embedding.Length != 1280)
            throw new ArgumentException(
                $"Expected 1280-dimensional embedding, got {embedding.Length}");

        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // Use INSERT OR REPLACE for upsert behavior
        command.CommandText = @"
            INSERT OR REPLACE INTO track_embeddings (track_id, embedding, created_at)
            VALUES ($1, $2, CURRENT_TIMESTAMP)";

        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));
        command.Parameters.Add(new DuckDBParameter(embedding));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasEmbeddingAsync(Guid trackId)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) FROM track_embeddings WHERE track_id = $1";
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));

        var count = (long)await command.ExecuteScalarAsync();
        return count > 0;
    }

    public async Task<List<(Guid TrackId, double Distance)>> GetSimilarTracksAsync(
        Guid trackId, int limit = 100, double maxDistance = 0.2)
    {
        using var connection = new DuckDBConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();

        // First, get the embedding for the source track
        command.CommandText = @"
            SELECT embedding FROM track_embeddings WHERE track_id = $1";
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));

        var result = await command.ExecuteScalarAsync();
        if (result == null)
        {
            _logger.LogWarning("No embedding found for track {TrackId}", trackId);
            return new List<(Guid, double)>();
        }

        var sourceEmbedding = (float[])result;

        // Find similar tracks using array_cosine_distance
        // Note: If HNSW index exists, DuckDB will use it automatically
        command.CommandText = @"
            SELECT
                track_id,
                array_cosine_distance(embedding, $1::FLOAT[1280]) AS distance
            FROM track_embeddings
            WHERE track_id != $2
            ORDER BY distance ASC
            LIMIT $3";

        command.Parameters.Clear();
        command.Parameters.Add(new DuckDBParameter(sourceEmbedding));
        command.Parameters.Add(new DuckDBParameter(trackId.ToString()));
        command.Parameters.Add(new DuckDBParameter(limit));

        var results = new List<(Guid, double)>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader.GetString(0));
            var distance = reader.GetDouble(1);

            // Filter by max distance
            if (distance <= maxDistance)
            {
                results.Add((id, distance));
            }
        }

        return results;
    }
}
```

**3.2 Register Service**

File: `src/Coral.Api/ServiceCollectionExtensions.cs` (or wherever services are registered)

```csharp
services.AddSingleton<IDuckDbEmbeddingService, DuckDbEmbeddingService>();

// Initialize DuckDB on startup
var serviceProvider = services.BuildServiceProvider();
var duckDbService = serviceProvider.GetRequiredService<IDuckDbEmbeddingService>();
await duckDbService.InitializeAsync();
```

---

### Phase 4: Get Tests Running (Expect Failures)

**Goal**: Run tests to see what breaks (BulkExtensions will fail)

**4.1 Run Test Suite**

```bash
dotnet test
```

**Expected Result**: Tests will **FAIL** with errors like:
```
System.IO.FileNotFoundException: Could not load file or assembly 'Npgsql'
  at Coral.BulkExtensions.BulkInsertContext.BulkInsertWithPostgresAsync(...)
```

**Why?** BulkExtensions still has Postgres-specific code that won't compile after removing Npgsql packages.

**4.2 Identify Failures**

Tests will fail in methods using:
- `NpgsqlConnection` type casting
- `BeginBinaryImportAsync()`
- `NpgsqlParameter` and `NpgsqlDbType`
- PostgreSQL COPY commands
- UNNEST queries

This is **expected and good** - failures guide what needs to be rewritten in Phase 5.

---

### Phase 5: Rewrite BulkExtensions for SQLite

**Goal**: Complete replacement of Postgres bulk insert code

**5.1 Remove ALL Postgres Code from BulkInsertContext.cs**

File: `src/Coral.BulkExtensions/BulkInsertContext.cs`

**DELETE** the following methods entirely:
1. `BulkInsertWithPostgresAsync` (~line 378)
2. `MapClrToNpgsqlType` (~line 479)
3. `BulkInsertJunctionRecordsAsync` (the one calling Postgres code)

Also delete:
- All `using Npgsql;` statements
- All `using NpgsqlTypes;` statements
- The `_typeToNpgsqlTypeCache` dictionary

**5.2 Replace BulkInsertEntitiesOfTypeAsync**

File: `src/Coral.BulkExtensions/BulkInsertContext.cs` (line ~352)

**REPLACE** the entire method with SQLite-only version:

```csharp
private async Task BulkInsertEntitiesOfTypeAsync(
    Type entityType,
    BulkInsertStats stats,
    CancellationToken ct)
{
    var cache = (IEntityCache)_entityCaches[entityType];
    var newEntities = cache.GetNewEntitiesUntyped();

    if (newEntities.Count == 0)
        return;

    _logger?.LogInformation("Bulk inserting {Count:N0} {Type} entities...",
        newEntities.Count, entityType.Name);

    var sw = System.Diagnostics.Stopwatch.StartNew();

    // SQLite bulk insert (no provider abstraction)
    await BulkInsertWithSqliteAsync(entityType, newEntities, ct);

    stats.EntitiesInserted[entityType] = newEntities.Count;

    _logger?.LogInformation("✓ Inserted {Count:N0} {Type} in {Time:F2}s ({Rate:N0} entities/sec)",
        newEntities.Count, entityType.Name, sw.Elapsed.TotalSeconds,
        newEntities.Count / sw.Elapsed.TotalSeconds);
}
```

**5.3 Add PRAGMA Optimizations**

**ADD** these PRAGMA settings at the **start** of bulk operations (before first insert):

```csharp
private async Task OptimizeSqliteForBulkInsert(DbConnection connection)
{
    // WAL mode: Write-Ahead Logging (safer than OFF, much faster than DELETE)
    await ExecutePragmaAsync(connection, "PRAGMA journal_mode = WAL");

    // NORMAL: fsync only at critical moments (safe with WAL)
    await ExecutePragmaAsync(connection, "PRAGMA synchronous = NORMAL");

    // 64MB cache (vs default ~2MB)
    await ExecutePragmaAsync(connection, "PRAGMA cache_size = -64000");

    // Store temp tables in memory
    await ExecutePragmaAsync(connection, "PRAGMA temp_store = MEMORY");

    // Optional: Exclusive locking if single connection
    // await ExecutePragmaAsync(connection, "PRAGMA locking_mode = EXCLUSIVE");
}

private async Task ExecutePragmaAsync(DbConnection connection, string pragma)
{
    using var command = connection.CreateCommand();
    command.CommandText = pragma;
    await command.ExecuteNonQueryAsync();
}
```

**Note**: These PRAGMAs can provide **2-4x speedup** based on benchmarks. WAL + NORMAL is production-safe.

**5.4 Implement BulkInsertWithSqliteAsync**

**ADD** this new method to `BulkInsertContext.cs`:

```csharp
private async Task BulkInsertWithSqliteAsync(
    Type entityType,
    System.Collections.IList entities,
    CancellationToken ct)
{
    if (entities.Count == 0)
        return;

    // Get entity metadata from EF Core
    var efEntityType = _dbContext.Model.FindEntityType(entityType);
    if (efEntityType == null)
        throw new InvalidOperationException(
            $"Entity type {entityType.Name} not found in DbContext model");

    var tableName = efEntityType.GetTableName();
    var properties = efEntityType.GetProperties()
        .Where(p => !p.IsShadowProperty() && p.GetColumnName() != null)
        .ToList();

    var columnNames = properties.Select(p => p.GetColumnName()).ToList();
    var paramPlaceholders = string.Join(", ",
        Enumerable.Range(0, columnNames.Count).Select(i => $"@p{i}"));

    var insertSql = $@"
        INSERT INTO ""{tableName}"" ({string.Join(", ", columnNames.Select(c => $"\"{c}\"""))})
        VALUES ({paramPlaceholders})";

    var connection = _dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync(ct);

    // Use ADO.NET for optimal performance
    using var command = connection.CreateCommand();
    command.CommandText = insertSql;

    // Create parameters once and reuse (critical for performance)
    var parameters = properties.Select((_, i) =>
    {
        var param = command.CreateParameter();
        param.ParameterName = $"@p{i}";
        command.Parameters.Add(param);
        return param;
    }).ToArray();

    // Batch inserts in transactions (optimal: ~1000 rows per transaction)
    var batches = entities.Cast<object>().Chunk(_options.EntityBatchSize);

    foreach (var batch in batches)
    {
        using var transaction = await connection.BeginTransactionAsync(ct);
        command.Transaction = transaction;

        foreach (var entity in batch)
        {
            // Set parameter values (reusing parameter objects)
            for (int i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var value = GetPropertyValue(entity, property);
                parameters[i].Value = value ?? DBNull.Value;
            }

            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}

private object? GetPropertyValue(object entity, Microsoft.EntityFrameworkCore.Metadata.IProperty property)
{
    var value = property.PropertyInfo?.GetValue(entity);
    var clrType = property.ClrType;

    // Handle null foreign keys - try to get value from navigation property
    if (value == null || (value is Guid guidValue && guidValue == Guid.Empty))
    {
        var foreignKeys = property.GetContainingForeignKeys();
        if (foreignKeys.Any())
        {
            var foreignKey = foreignKeys.First();
            var navigation = foreignKey.DependentToPrincipal;

            if (navigation?.PropertyInfo != null)
            {
                var navigationValue = navigation.PropertyInfo.GetValue(entity);
                if (navigationValue != null)
                {
                    value = navigationValue.GetType().GetProperty("Id")?.GetValue(navigationValue);
                }
            }
        }
    }

    // Handle enums - convert to underlying integer type
    var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;
    if (underlyingType.IsEnum && value != null)
    {
        value = Convert.ToInt32(value);
    }

    return value;
}
```

**When to call OptimizeSqliteForBulkInsert:**
```csharp
// Call ONCE at the start of BulkInsertAsync (before any inserts)
public async Task BulkInsertAsync(CancellationToken ct = default)
{
    var connection = _dbContext.Database.GetDbConnection();
    if (connection.State != ConnectionState.Open)
        await connection.OpenAsync(ct);

    // Apply optimizations once
    await OptimizeSqliteForBulkInsert(connection);

    // ... rest of bulk insert logic
}
```

**5.5 Replace BulkInsertRelationshipsAsync**

File: `src/Coral.BulkExtensions/BulkInsertContext.cs` (line ~487)

**REPLACE** the entire method with SQLite-only version:

```csharp
private async Task BulkInsertRelationshipsAsync(
    BulkInsertStats stats,
    CancellationToken ct)
{
    if (_relationships.Count == 0)
        return;

    var groupedRelationships = _relationships
        .GroupBy(r => r.JunctionInfo, new JunctionTableInfoComparer())
        .ToList();

    foreach (var group in groupedRelationships)
    {
        var junctionInfo = group.Key;
        var records = group
            .Select(r => (Left: r.LeftKey, Right: r.RightKey))
            .ToList();

        if (records.Count == 0)
            continue;

        _logger?.LogInformation("Bulk inserting {Count:N0} {Relationship} relationships...",
            records.Count,
            $"{junctionInfo.LeftType.Name} <-> {junctionInfo.RightType.Name}");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // SQLite bulk insert (no provider abstraction)
        await BulkInsertJunctionRecordsSqliteAsync(junctionInfo, records, ct);

        var relationshipKey = $"{junctionInfo.LeftType.Name} <-> {junctionInfo.RightType.Name}";
        stats.RelationshipsInserted[relationshipKey] = records.Count;

        _logger?.LogInformation("✓ Inserted {Count:N0} relationships in {Time:F2}s ({Rate:N0} records/sec)",
            records.Count, sw.Elapsed.TotalSeconds,
            records.Count / sw.Elapsed.TotalSeconds);
    }
}
```

**5.6 Implement BulkInsertJunctionRecordsSqliteAsync**

**ADD** this new method to `BulkInsertContext.cs`:

```csharp
private async Task BulkInsertJunctionRecordsSqliteAsync(
    JunctionTableInfo junctionInfo,
    List<(Guid Left, Guid Right)> records,
    CancellationToken ct)
{
    var fullTableName = $"\"{junctionInfo.TableName}\"";

    // SQLite uses INSERT OR IGNORE instead of ON CONFLICT DO NOTHING
    var sql = $@"
        INSERT OR IGNORE INTO {fullTableName}
        (""{junctionInfo.LeftColumnName}"", ""{junctionInfo.RightColumnName}"")
        VALUES (@left, @right)";

    var connection = _dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync(ct);

    using var command = connection.CreateCommand();
    command.CommandText = sql;

    // Create parameters once and reuse
    var leftParam = command.CreateParameter();
    leftParam.ParameterName = "@left";
    var rightParam = command.CreateParameter();
    rightParam.ParameterName = "@right";
    command.Parameters.Add(leftParam);
    command.Parameters.Add(rightParam);

    // Batch in chunks with transactions
    var chunks = records.Chunk(_options.JunctionBatchSize);

    foreach (var chunk in chunks)
    {
        using var transaction = await connection.BeginTransactionAsync(ct);
        command.Transaction = transaction;

        foreach (var (left, right) in chunk)
        {
            leftParam.Value = left;
            rightParam.Value = right;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
```

---

### Phase 6: Validate All Tests Pass

**Goal**: Ensure SQLite migration is complete and stable

**6.1 Run Full Test Suite**

```bash
dotnet test
```

**Expected Result**: All tests should **PASS** ✅

**6.2 Verify Test Performance**

Tests should now run **50-100x faster** than before:
- Before (Testcontainers): ~5-10 seconds startup
- After (in-memory SQLite): <100ms startup

**6.3 Checkpoint: SQLite Migration Complete**

If all tests pass, you now have:
- ✅ SQLite transactional database working
- ✅ BulkExtensions rewritten for SQLite
- ✅ All tests passing with in-memory SQLite
- ✅ Zero Docker dependencies

**🎉 SQLite migration is DONE! Now we can add DuckDB embeddings.**

---

### Phase 7: Introduce DuckDB Dependency

**Goal**: Add DuckDB packages (but don't use yet)

**7.1 Add NuGet Package**

File: `src/Coral.Services/Coral.Services.csproj`

**Add:**
```xml
<PackageReference Include="DuckDB.NET.Data.Full" Version="1.4.1" />
```

**7.2 Update ApplicationConfiguration**

File: `src/Coral.Configuration/ApplicationConfiguration.cs`

**Add:**
```csharp
public static string DuckDbEmbeddingsPath => Path.Combine(DataDirectory, "embeddings.db");
```

**7.3 Verify Build**

```bash
dotnet build
```

All projects should build successfully.

---

### Phase 8: Create DuckDB Embedding Service

**5.1 Update EmbeddingWorker**

File: `src/Coral.Api/Workers/EmbeddingWorker.cs`

```csharp
// Add DuckDB service to constructor
private readonly IDuckDbEmbeddingService _duckDbService;

public EmbeddingWorker(
    IEmbeddingChannel channel,
    ILogger<EmbeddingWorker> logger,
    IServiceScopeFactory scopeFactory,
    InferenceService inferenceService,
    IDuckDbEmbeddingService duckDbService)  // NEW
{
    _channel = channel;
    _logger = logger;
    _scopeFactory = scopeFactory;
    _inferenceService = inferenceService;
    _duckDbService = duckDbService;  // NEW
}

private async Task GetEmbeddings(CancellationToken stoppingToken, EmbeddingJob job)
{
    var track = job.Track;
    var sw = Stopwatch.StartNew();

    // ... existing duration validation ...

    await _semaphore.WaitAsync(stoppingToken);
    try
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reporter = scope.ServiceProvider.GetRequiredService<IScanReporter>();

        // Check if embedding already exists
        if (await _duckDbService.HasEmbeddingAsync(track.Id))
            return;

        // Run inference
        var embeddings = await _inferenceService.RunInference(track.AudioFile.FilePath);

        // OLD: Store in PostgreSQL
        // await context.TrackEmbeddings.AddAsync(new TrackEmbedding()
        // {
        //     CreatedAt = DateTime.UtcNow,
        //     Embedding = new Vector(embeddings),
        //     TrackId = track.Id
        // }, stoppingToken);
        // await context.SaveChangesAsync(stoppingToken);

        // NEW: Store in DuckDB
        await _duckDbService.InsertEmbeddingAsync(track.Id, embeddings);

        _logger.LogInformation("Stored embeddings for track {FilePath} in {Time:F2}s",
            track.AudioFile.FilePath, sw.Elapsed.TotalSeconds);

        await reporter.ReportEmbeddingCompleted(job.RequestId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get embeddings for track: {Path}",
            track.AudioFile.FilePath);
    }
    finally
    {
        _semaphore.Release();
    }
}
```

**5.2 Update LibraryService.GetRecommendationsForTrack**

File: `src/Coral.Services/LibraryService.cs`

```csharp
// Add DuckDB service to constructor
private readonly IDuckDbEmbeddingService _duckDbService;

public LibraryService(
    CoralDbContext context,
    IMapper mapper,
    IScanChannel scanChannel,
    ILogger<LibraryService> logger,
    IDuckDbEmbeddingService duckDbService)  // NEW
{
    _context = context;
    _mapper = mapper;
    _scanChannel = scanChannel;
    _logger = logger;
    _duckDbService = duckDbService;  // NEW
}

public async Task<List<SimpleTrackDto>> GetRecommendationsForTrack(Guid trackId)
{
    // OLD: Query PostgreSQL with pgvector
    // var trackEmbeddings = await _context.TrackEmbeddings
    //     .FirstOrDefaultAsync(t => t.TrackId == trackId);
    // if (trackEmbeddings == null)
    //     return [];
    //
    // await using var transaction = await _context.Database.BeginTransactionAsync();
    // await _context.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.ef_search = 100");
    //
    // var recs = await _context.TrackEmbeddings
    //     .Select(t => new {
    //         Entity = t,
    //         Distance = t.Embedding.CosineDistance(trackEmbeddings.Embedding)
    //     })
    //     .OrderBy(t => t.Distance)
    //     .Take(100)
    //     .ToListAsync();
    //
    // await transaction.CommitAsync();

    // NEW: Query DuckDB
    var similarTracks = await _duckDbService.GetSimilarTracksAsync(
        trackId,
        limit: 100,
        maxDistance: 0.2);

    if (!similarTracks.Any())
        return new List<SimpleTrackDto>();

    // Get track IDs (filtering by distance already done in DuckDB service)
    var trackIds = similarTracks
        .DistinctBy(t => t.Distance)  // Identical distances = duplicates
        .Select(t => t.TrackId)
        .ToList();

    // Fetch full track info from SQLite
    var tracks = await _context.Tracks
        .Where(t => trackIds.Contains(t.Id))
        .ProjectTo<SimpleTrackDto>(_mapper.ConfigurationProvider)
        .ToListAsync();

    // Maintain order from similarity results
    var trackDict = tracks.ToDictionary(t => t.Id);
    var orderedTracks = trackIds
        .Where(id => trackDict.ContainsKey(id))
        .Select(id => trackDict[id])
        .ToList();

    return orderedTracks;
}
```

**5.3 Remove Pgvector References**

Find and remove all references to `Pgvector` namespace:

```bash
# Find all usages
grep -r "using Pgvector" src/
grep -r "Pgvector.EntityFrameworkCore" src/

# Files to update:
# - src/Coral.Services/LibraryService.cs (remove using)
# - src/Coral.Api/Workers/EmbeddingWorker.cs (remove using)
# - src/Coral.Database.Models/TrackEmbedding.cs (delete entire file)
```

---

### Phase 6: Test Infrastructure Migration

**6.1 Current Test Setup**

Coral uses Testcontainers to spin up PostgreSQL instances for integration tests:

```csharp
// Current: Coral.TestProviders/DatabaseFixture.cs
public class SharedPostgresContainer
{
    private static PostgreSqlContainer? _container;

    public static async Task<PostgreSqlContainer> GetContainerAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:0.8.1-pg17-trixie")
            .Build();
        await _container.StartAsync();
        return _container;
    }
}

public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    public TestDatabase TestDb { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = await SharedPostgresContainer.GetContainerAsync();
        TestDb = new TestDatabase(opt =>
        {
            opt.UseNpgsql(_container.GetConnectionString(), p => p.UseVector());
        });
    }
}
```

**Problems:**
- ✗ Docker required to run tests
- ✗ Slow test startup (container spin-up)
- ✗ Complex CI/CD setup
- ✗ Postgres-specific dependencies

**6.2 Update Coral.TestProviders.csproj**

File: `tests/Coral.TestProviders/Coral.TestProviders.csproj`

**Remove:**
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4"/>
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.2"/>
<PackageReference Include="Testcontainers" Version="4.7.0"/>
<PackageReference Include="Testcontainers.PostgreSql" Version="4.7.0"/>
<PackageReference Include="Testcontainers.Xunit" Version="4.7.0"/>
```

**Add:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0"/>
```

**6.3 Replace DatabaseFixture with In-Memory SQLite**

File: `tests/Coral.TestProviders/DatabaseFixture.cs`

```csharp
using Coral.Configuration;
using Coral.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Coral.TestProviders;

public class DatabaseFixture : IAsyncLifetime
{
    private SqliteConnection? _connection;
    public TestDatabase TestDb { get; private set; } = null!;

    private void CleanUpTempLibraries()
    {
        var libraries = TestDb.Context.MusicLibraries
            .Where(l => l.LibraryPath != "");
        foreach (var library in libraries)
        {
            if (!Guid.TryParse(Path.GetFileName(library.LibraryPath), out _)) continue;
            var directory = new DirectoryInfo(library.LibraryPath);
            foreach (var file in directory.EnumerateFiles("*.*", SearchOption.AllDirectories))
            {
                file.Delete();
            }

            foreach (var directoryInLibrary in directory.EnumerateDirectories("*.*", SearchOption.AllDirectories))
            {
                directoryInLibrary.Delete();
            }

            directory.Delete();
        }
    }

    private void CleanUpArtwork()
    {
        var indexedArtwork = TestDb.Context.Artworks
            .Where(a => a.Path.StartsWith(ApplicationConfiguration.Thumbnails)
                        || a.Path.StartsWith(ApplicationConfiguration.ExtractedArtwork))
            .Select(a => a.Path);

        foreach (var artworkPath in indexedArtwork)
        {
            try
            {
                var directory = new DirectoryInfo(artworkPath).Parent;
                File.Delete(artworkPath);
                if (!directory!.GetFiles().Any())
                {
                    directory.Delete();
                }
            }
            catch (Exception) { }
        }
    }

    public async Task InitializeAsync()
    {
        // Create in-memory SQLite connection
        // IMPORTANT: Must keep connection open for in-memory database to persist
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        TestDb = new TestDatabase(opt =>
        {
            opt.UseSqlite(_connection);
        });

        // No Testcontainers, no Docker - instant startup!
    }

    public Task DisposeAsync()
    {
        CleanUpArtwork();
        CleanUpTempLibraries();
        TestDb?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
        return Task.CompletedTask;
    }
}
```

**Key Changes:**
- ✅ Replace `PostgreSqlContainer` with `SqliteConnection`
- ✅ Use `Data Source=:memory:` for in-memory database
- ✅ Keep connection open (in-memory DB destroyed when connection closes)
- ✅ Remove all Testcontainers code
- ✅ Remove `SharedPostgresContainer` class entirely

**6.4 Update TestDatabase**

File: `tests/Coral.TestProviders/TestDatabase.cs`

No code changes needed! TestDatabase accepts a `DbContextOptionsBuilder` action, so it works with both providers:

```csharp
public TestDatabase(Action<DbContextOptionsBuilder> optionsAction)
{
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddDbContext<CoralDbContext>(optionsAction);  // Works with SQLite!
    // ... rest of initialization
}
```

**6.5 Benefits of In-Memory SQLite Tests**

| Aspect | PostgreSQL Testcontainers | In-Memory SQLite | Improvement |
|--------|--------------------------|------------------|-------------|
| **Startup time** | ~5-10 seconds (Docker) | <100ms | **50-100x faster** |
| **Dependencies** | Docker, Testcontainers | None | **Zero dependencies** |
| **CI/CD** | Docker-in-Docker setup | Native | **Simplified** |
| **Isolation** | Shared container | Per-test database | **Better isolation** |
| **Parallel tests** | Limited | Unlimited | **Full parallelism** |

**6.6 Test Compatibility**

All existing tests should work without modification because:
- ✅ Test data structure unchanged (TestDatabase)
- ✅ xUnit fixture pattern unchanged (IClassFixture)
- ✅ DbContext usage unchanged
- ✅ SQLite supports all features used in tests (no pgvector in test data)

**6.7 Run Tests**

```bash
# Run all tests (no Docker required!)
dotnet test

# Run specific test project
dotnet test tests/Coral.Services.Tests
dotnet test tests/Coral.BulkExtensions.Tests
dotnet test tests/Coral.Dto.Tests

# Tests now run 50-100x faster!
```

---

## Migration Execution Checklist

Use this checklist when performing the actual migration:

### Pre-Migration

- [ ] **Backup existing PostgreSQL database**
  ```bash
  pg_dump coral2 > coral_backup_$(date +%Y%m%d).sql
  ```

- [ ] **Create backup of codebase**
  ```bash
  git checkout -b migration/postgres-to-embedded
  git add -A
  git commit -m "Checkpoint before database migration"
  ```

### Phase 1: Setup

- [ ] Update NuGet packages (remove Npgsql, add SQLite & DuckDB)
- [ ] Add database path configuration
- [ ] Verify all packages restore successfully

### Phase 2: SQLite

- [ ] Update `CoralDbContext.OnConfiguring` to use SQLite
- [ ] Remove `TrackEmbedding` DbSet
- [ ] Clean `OnModelCreating` (remove Postgres extensions)
- [ ] Delete all existing migrations
- [ ] Create new `InitialSqlite` migration
- [ ] Test: `dotnet ef database update --project src/Coral.Database`

### Phase 3: DuckDB

- [ ] Create `DuckDbEmbeddingService.cs`
- [ ] Register service in DI container
- [ ] Add initialization call on application startup
- [ ] Test: Run app and verify DuckDB initializes without errors

### Phase 4: BulkExtensions

- [ ] Add provider detection in `BulkInsertEntitiesOfTypeAsync`
- [ ] Implement `BulkInsertWithSqliteAsync`
- [ ] Rename existing method to `BulkInsertJunctionRecordsPostgresAsync`
- [ ] Implement `BulkInsertJunctionRecordsSqliteAsync`
- [ ] Add `GetPropertyValue` helper method

### Phase 5: Services

- [ ] Update `EmbeddingWorker` to inject `IDuckDbEmbeddingService`
- [ ] Modify `GetEmbeddings` to write to DuckDB
- [ ] Update `LibraryService` constructor
- [ ] Rewrite `GetRecommendationsForTrack` to query DuckDB
- [ ] Remove all `using Pgvector` statements
- [ ] Delete `TrackEmbedding.cs` model file

### Phase 6: Test Infrastructure

- [ ] Update `Coral.TestProviders.csproj` (remove Testcontainers, add SQLite)
- [ ] Replace `DatabaseFixture.cs` with in-memory SQLite implementation
- [ ] Delete `SharedPostgresContainer` class
- [ ] Keep connection open in fixture (required for `:memory:` database)
- [ ] Test: `dotnet test` (all tests should pass, 50-100x faster)

### Testing

- [ ] **Clean start test**
  ```bash
  rm -rf ~/coral/data/*.db
  dotnet run --project src/Coral.Cli
  ```

- [ ] **Index small library** (10-20 tracks)
  ```bash
  # Watch logs for successful indexing
  ```

- [ ] **Verify SQLite data**
  ```bash
  sqlite3 ~/coral/data/coral.db
  > SELECT COUNT(*) FROM Tracks;
  > SELECT COUNT(*) FROM Albums;
  > SELECT COUNT(*) FROM Artists;
  ```

- [ ] **Verify DuckDB embeddings**
  ```bash
  duckdb ~/coral/data/embeddings.db
  D SELECT COUNT(*) FROM track_embeddings;
  D DESCRIBE track_embeddings;
  ```

- [ ] **Test recommendations API**
  ```bash
  # Get a track ID
  curl http://localhost:7214/api/tracks | jq '.[0].id'

  # Get recommendations
  curl http://localhost:7214/api/tracks/{id}/recommendations
  ```

- [ ] **Performance test** (index larger library, 100+ tracks)
- [ ] **Verify HNSW index creation** (after >1000 embeddings)

### Post-Migration

- [ ] Remove Npgsql/Pgvector NuGet packages
- [ ] Update documentation
- [ ] Update README.md (remove PostgreSQL setup instructions)
- [ ] Commit changes
  ```bash
  git add -A
  git commit -m "feat: migrate from PostgreSQL to SQLite + DuckDB"
  ```

---

## Performance Expectations

### Bulk Insert Performance

| Operation | PostgreSQL (COPY) | SQLite (Transaction Batch) | Expected Impact |
|-----------|-------------------|---------------------------|-----------------|
| 10,000 tracks | ~2-3 seconds | ~5-8 seconds | Acceptable (indexing is I/O bound) |
| Junction inserts | ~1 second | ~2-3 seconds | Minimal impact |

**Bottleneck**: Disk I/O for reading audio files and running inference, not database writes.

### Query Performance

| Operation | PostgreSQL | SQLite | Expected Impact |
|-----------|-----------|---------|-----------------|
| Point lookups (track by ID) | <1ms | <1ms | No change |
| Album/artist queries | ~5ms | ~3ms | Slightly faster (B-tree) |
| Browse library | ~10ms | ~8ms | Slightly faster |

### Vector Search Performance

| Operation | pgvector (HNSW) | DuckDB VSS (HNSW) | Expected Impact |
|-----------|-----------------|-------------------|-----------------|
| Find 100 similar tracks | ~50ms | ~50-100ms | Comparable |
| Cold start (no index) | ~200ms | ~200ms | Comparable |

**Note**: DuckDB's VSS extension uses similar HNSW implementation to pgvector.

---

## Risks & Mitigation

### Risk 1: SQLite Write Performance

**Risk**: SQLite 10-500x slower than Postgres for writes
**Impact**: High - affects indexing speed
**Mitigation**:
- Use transaction batching (proven 1000x speedup)
- Batch size ~1000 rows per transaction
- Indexing is already I/O-bound (audio file reading), not DB-bound

**Verdict**: **Low concern** - bulk insert is fast enough for the use case

### Risk 2: DuckDB VSS Extension Not Loading

**Risk**: Extension fails to install/load
**Impact**: High - no embeddings = no recommendations
**Mitigation**:
- Auto-install in `InitializeAsync`
- Catch and log errors clearly
- Fail fast with helpful error message

**Test**:
```csharp
try {
    await duckDbService.InitializeAsync();
} catch (Exception ex) {
    logger.LogError("FATAL: DuckDB VSS extension failed to load. " +
        "Ensure DuckDB.NET.Data.Full is installed. Error: {Error}", ex.Message);
    throw;
}
```

### Risk 3: Embedding Dimensionality Mismatch

**Risk**: Coral.Essentia.Cli outputs wrong dimension count
**Impact**: Medium - crashes during insert
**Mitigation**:
- Validate embedding size at insertion (already in code)
- Throw clear error message
- Log expected vs actual dimensions

**Already handled**:
```csharp
if (embedding.Length != 1280)
    throw new ArgumentException(
        $"Expected 1280-dimensional embedding, got {embedding.Length}");
```

### Risk 4: Data Loss During Migration

**Risk**: Migration fails, original data lost
**Impact**: Critical
**Mitigation**:
- **ALWAYS** backup PostgreSQL database before migration
- Test on copy of database first
- Keep PostgreSQL connection code temporarily (easy rollback)

**Backup command**:
```bash
pg_dump coral2 > coral_backup_$(date +%Y%m%d).sql
```

### Risk 5: HNSW Index Creation Slow

**Risk**: Creating index on large dataset (>100k embeddings) takes too long
**Impact**: Low - one-time operation
**Mitigation**:
- Only create index after bulk insert completes
- Log progress to user
- Skip index creation for <1000 embeddings (linear scan is fast enough)

**Current approach**: Check count, only create index if >1000 rows

---

## Rollback Plan

If migration fails, rollback steps:

1. **Revert code changes**
   ```bash
   git reset --hard HEAD~1
   ```

2. **Switch back to PostgreSQL**
   - Restore `Npgsql` packages
   - Revert `CoralDbContext` to use `UseNpgsql`
   - Restore migrations from backup

3. **Restore PostgreSQL data** (if needed)
   ```bash
   psql -U postgres -d coral2 < coral_backup_YYYYMMDD.sql
   ```

4. **Keep SQLite/DuckDB code** (optional)
   - Provider abstraction allows both to coexist
   - Can switch via configuration flag

---

## Future Enhancements (Post-Migration)

Once the migration is stable:

1. **Search Refactor** (next phase)
   - Move search to DuckDB FTS extension
   - BM25 ranking for search results
   - Denormalized search index

2. **Hybrid Database Sync**
   - Background job to sync SQLite changes to DuckDB search index
   - Track last sync timestamp

3. **Optimize DuckDB Queries**
   - Experiment with different HNSW parameters
   - Benchmark index build time vs query speed

4. **Export/Import Tools**
   - Export SQLite database for backup
   - Import existing music metadata from PostgreSQL dump

---

## Conclusion

This migration eliminates the external PostgreSQL dependency, simplifying deployment for end users while maintaining excellent performance for Coral's workload. The dual-database architecture plays to each database's strengths:

- **SQLite** handles transactional operations (99% of queries)
- **DuckDB** handles analytical operations (vector similarity)

**Additional Benefits:**
- ✅ **Simplified testing**: In-memory SQLite replaces Docker Testcontainers
- ✅ **50-100x faster test execution**: No container startup overhead
- ✅ **Zero external dependencies**: No PostgreSQL server, no Docker required
- ✅ **Easier CI/CD**: Native test execution without Docker-in-Docker complexity

**Expected timeline**: 2-3 days for implementation + testing

**Next steps**:
1. Start with Phase 1 (Infrastructure Setup)
2. Follow checklist sequentially
3. Test thoroughly at each phase
4. Defer search refactor until migration is stable
