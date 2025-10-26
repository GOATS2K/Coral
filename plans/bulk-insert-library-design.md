# Coral Bulk Insert Library - API Design

## Overview

A generic EF Core extension library for efficient bulk insertion with automatic many-to-many relationship handling, entity caching, and dependency resolution.

**Key Design Goals:**
1. Minimal changes to IndexerService - drop-in replacement for standard EF Core operations
2. Automatic junction table resolution using EF Core metadata
3. Built-in caching with `GetOrAdd()` semantics
4. Automatic dependency ordering for bulk inserts
5. Raw SQL optimization for junction tables
6. Type-safe, generic API

**Quick Example:**
```csharp
await _context.BulkOperationAsync(async bulkContext =>
{
    var genre = await bulkContext.GetOrAddAsync(
        keySelector: g => g.Name,
        createFunc: () => new Genre
        {
            Id = Guid.NewGuid(),
            Name = "Rock",
            CreatedAt = DateTime.UtcNow
        });

    var track = await bulkContext.GetOrAddAsync(
        keySelector: t => t.FilePath,
        createFunc: () => new Track
        {
            Id = Guid.NewGuid(),
            FilePath = "/path/to/track.mp3"
        });

    bulkContext.RegisterRelationship(track, genre);

    // Explicit SaveChangesAsync required
});
```

---

## Core API Design

### 1. BulkInsertContext - Main Entry Point

```csharp
/// <summary>
/// Context for batching entity operations and relationships for bulk insertion.
/// Wraps a DbContext and provides caching, relationship tracking, and bulk save operations.
/// </summary>
public sealed class BulkInsertContext : IAsyncDisposable
{
    private readonly DbContext _dbContext;
    private readonly ILogger? _logger;
    private readonly Dictionary<Type, object> _entityCaches = new();
    private readonly List<RelationshipRegistration> _relationships = new();

    internal BulkInsertContext(DbContext dbContext, BulkInsertOptions? options = null)
    {
        _dbContext = dbContext;
        _logger = options?.Logger;
        // Initialize...
    }

    /// <summary>
    /// Gets an existing entity from cache/DB or adds a new one.
    /// Similar to ConcurrentDictionary.GetOrAdd() semantics.
    /// </summary>
    /// <param name="key">The key value to search for (e.g., "Rock" for a genre name)</param>
    /// <param name="keySelector">Expression to extract the key from an entity (e.g., g => g.Name)</param>
    /// <param name="createFunc">Function to create a new entity if not found</param>
    public async Task<TEntity> GetOrAddAsync<TEntity>(
        object key,
        Expression<Func<TEntity, object>> keySelector,
        Func<TEntity> createFunc)
        where TEntity : class
    {
        // Implementation below...
    }

    /// <summary>
    /// Registers a many-to-many relationship between two entities.
    /// Junction table info is resolved automatically from EF Core metadata.
    /// </summary>
    public void RegisterRelationship<TLeft, TRight>(TLeft left, TRight right)
        where TLeft : class
        where TRight : class
    {
        // Implementation below...
    }

    /// <summary>
    /// Bulk saves all cached entities and relationships.
    /// Handles dependency ordering and junction table insertion automatically.
    /// </summary>
    public async Task<BulkInsertStats> SaveChangesAsync(CancellationToken ct = default)
    {
        // Implementation below...
    }

    /// <summary>
    /// Clears all caches and relationship registrations without saving.
    /// </summary>
    public void Clear()
    {
        _entityCaches.Clear();
        _relationships.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        // Auto-save on dispose? Or require explicit SaveChangesAsync()?
        // Recommendation: Require explicit save for predictability
    }
}
```

### 2. Extension Methods on DbContext

```csharp
public static class BulkInsertExtensions
{
    /// <summary>
    /// Creates a new BulkInsertContext for batching operations.
    /// </summary>
    public static BulkInsertContext CreateBulkContext(
        this DbContext context,
        BulkInsertOptions? options = null)
    {
        return new BulkInsertContext(context, options);
    }

    /// <summary>
    /// Alternative: Execute bulk operations within a scoped context.
    /// </summary>
    public static async Task<BulkInsertStats> BulkOperationAsync(
        this DbContext context,
        Func<BulkInsertContext, Task> operation,
        BulkInsertOptions? options = null,
        CancellationToken ct = default)
    {
        await using var bulkContext = context.CreateBulkContext(options);
        await operation(bulkContext);
        return await bulkContext.SaveChangesAsync(ct);
    }
}
```

### 3. Configuration Options

```csharp
public class BulkInsertOptions
{
    /// <summary>
    /// Logger for diagnostics and performance metrics.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Batch size for junction table inserts (default: 50,000).
    /// </summary>
    public int JunctionBatchSize { get; set; } = 50_000;

    /// <summary>
    /// Whether to set output identity on bulk inserts (default: true).
    /// Required for entities with auto-generated keys.
    /// </summary>
    public bool SetOutputIdentity { get; set; } = true;

    /// <summary>
    /// Whether to disable auto-detect changes during bulk operations (default: true).
    /// Improves performance but requires manual tracking.
    /// </summary>
    public bool DisableAutoDetectChanges { get; set; } = true;

    /// <summary>
    /// Custom key selector functions for entity types.
    /// If not specified, uses EF Core's key metadata.
    /// </summary>
    public Dictionary<Type, Func<object, object>> KeySelectors { get; } = new();
}
```

### 4. Statistics and Diagnostics

```csharp
public class BulkInsertStats
{
    public Dictionary<Type, int> EntitiesInserted { get; } = new();
    public Dictionary<Type, int> EntitiesUpdated { get; } = new();
    public Dictionary<string, int> RelationshipsInserted { get; } = new(); // Key: "Track <-> Artist"

    public int TotalEntitiesInserted => EntitiesInserted.Values.Sum();
    public int TotalEntitiesUpdated => EntitiesUpdated.Values.Sum();
    public int TotalRelationshipsInserted => RelationshipsInserted.Values.Sum();

    public TimeSpan EntityInsertionTime { get; set; }
    public TimeSpan RelationshipInsertionTime { get; set; }
    public TimeSpan TotalTime { get; set; }

    public void LogSummary(ILogger logger)
    {
        logger.LogInformation(
            "Bulk insert completed: {Entities} entities, {Relationships} relationships in {Time:F2}s",
            TotalEntitiesInserted,
            TotalRelationshipsInserted,
            TotalTime.TotalSeconds);

        foreach (var (type, count) in EntitiesInserted)
        {
            logger.LogInformation("  {Type}: {Count:N0} inserted", type.Name, count);
        }

        foreach (var (relationship, count) in RelationshipsInserted)
        {
            logger.LogInformation("  {Relationship}: {Count:N0} relationships", relationship, count);
        }
    }
}
```

---

## Implementation Details

### GetOrAdd Implementation

```csharp
public async Task<TEntity> GetOrAddAsync<TEntity>(
    object key,
    Expression<Func<TEntity, object>> keySelector,
    Func<TEntity> createFunc)
    where TEntity : class
{
    // Get or create typed cache for this entity type
    var cache = GetOrCreateCache<TEntity>();

    // Create a cache key that includes the selector to support multiple lookup strategies
    var cacheKey = (keySelector.ToString(), key);

    // Check cache first
    if (cache.TryGetValue(cacheKey, out var cached))
    {
        return cached.Entity;
    }

    // Query database using the provided selector (only once per unique key)
    var existing = await QueryEntityByKeySelectorAsync(key, keySelector);
    if (existing != null)
    {
        cache[cacheKey] = new CachedEntity<TEntity>(existing, isNew: false);
        return existing;
    }

    // Create new entity
    var newEntity = createFunc();
    cache[cacheKey] = new CachedEntity<TEntity>(newEntity, isNew: true);

    return newEntity;
}

private EntityCache<TEntity> GetOrCreateCache<TEntity>() where TEntity : class
{
    if (!_entityCaches.TryGetValue(typeof(TEntity), out var cache))
    {
        cache = new EntityCache<TEntity>();
        _entityCaches[typeof(TEntity)] = cache;
    }
    return (EntityCache<TEntity>)cache;
}

private async Task<TEntity?> QueryEntityByKeySelectorAsync<TEntity>(
    object key,
    Expression<Func<TEntity, object>> keySelector)
    where TEntity : class
{
    var dbSet = _dbContext.Set<TEntity>();

    // Build Where(e => keySelector(e) == key)
    var parameter = keySelector.Parameters[0]; // Reuse the parameter from keySelector
    var body = keySelector.Body;

    // Handle boxing conversion if present (when selector returns value type)
    if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
    {
        body = unary.Operand;
    }

    var constant = Expression.Constant(key, body.Type);
    var equals = Expression.Equal(body, constant);
    var lambda = Expression.Lambda<Func<TEntity, bool>>(equals, parameter);

    return await dbSet.AsNoTracking().FirstOrDefaultAsync(lambda);
}
```

### RegisterRelationship Implementation

```csharp
public void RegisterRelationship<TLeft, TRight>(TLeft left, TRight right)
    where TLeft : class
    where TRight : class
{
    // Validate entities are tracked in cache
    var leftCache = GetOrCreateCache<TLeft>();
    var rightCache = GetOrCreateCache<TRight>();

    if (!leftCache.ContainsEntity(left))
        throw new InvalidOperationException(
            $"Entity {typeof(TLeft).Name} must be added via GetOrAddAsync before registering relationships");

    if (!rightCache.ContainsEntity(right))
        throw new InvalidOperationException(
            $"Entity {typeof(TRight).Name} must be added via GetOrAddAsync before registering relationships");

    // Resolve junction table metadata from EF Core model
    var junctionInfo = ResolveJunctionTable<TLeft, TRight>();

    // Extract primary keys from entities
    var leftKey = GetPrimaryKey(left);
    var rightKey = GetPrimaryKey(right);

    // Store relationship for bulk insert
    _relationships.Add(new RelationshipRegistration(
        JunctionInfo: junctionInfo,
        LeftKey: leftKey,
        RightKey: rightKey
    ));
}

private JunctionTableInfo ResolveJunctionTable<TLeft, TRight>()
{
    var leftType = _dbContext.Model.FindEntityType(typeof(TLeft));
    var rightType = _dbContext.Model.FindEntityType(typeof(TRight));

    // Find the skip navigation (many-to-many relationship)
    var skipNavigation = leftType.GetSkipNavigations()
        .FirstOrDefault(n => n.TargetEntityType == rightType);

    if (skipNavigation == null)
    {
        skipNavigation = rightType.GetSkipNavigations()
            .FirstOrDefault(n => n.TargetEntityType == leftType);

        if (skipNavigation == null)
            throw new InvalidOperationException(
                $"No many-to-many relationship found between {typeof(TLeft).Name} and {typeof(TRight).Name}");
    }

    // Extract junction table metadata
    var joinEntityType = skipNavigation.JoinEntityType;
    var tableName = joinEntityType.GetTableName();
    var schema = joinEntityType.GetSchema();

    // Get foreign key column names
    var leftForeignKey = joinEntityType.GetForeignKeys()
        .First(fk => fk.PrincipalEntityType == leftType);
    var rightForeignKey = joinEntityType.GetForeignKeys()
        .First(fk => fk.PrincipalEntityType == rightType);

    var leftColumnName = leftForeignKey.Properties[0].GetColumnName();
    var rightColumnName = rightForeignKey.Properties[0].GetColumnName();

    return new JunctionTableInfo(
        TableName: tableName,
        Schema: schema,
        LeftColumnName: leftColumnName,
        RightColumnName: rightColumnName,
        LeftType: typeof(TLeft),
        RightType: typeof(TRight)
    );
}

private object GetPrimaryKey<TEntity>(TEntity entity) where TEntity : class
{
    var entityType = _dbContext.Model.FindEntityType(typeof(TEntity));
    var primaryKey = entityType.FindPrimaryKey();

    if (primaryKey.Properties.Count == 1)
    {
        var keyProperty = primaryKey.Properties[0].PropertyInfo;
        return keyProperty.GetValue(entity)!;
    }

    // For composite keys, return a tuple or custom object
    throw new NotSupportedException("Composite keys not yet supported");
}
```

### SaveChangesAsync Implementation

```csharp
public async Task<BulkInsertStats> SaveChangesAsync(CancellationToken ct = default)
{
    var stats = new BulkInsertStats();
    var totalSw = Stopwatch.StartNew();

    // Phase 1: Sort entities by dependency order
    var sortedEntityTypes = TopologicalSort();

    // Phase 2: Bulk insert entities in dependency order
    var entitySw = Stopwatch.StartNew();

    var originalAutoDetect = _dbContext.ChangeTracker.AutoDetectChangesEnabled;
    try
    {
        if (DisableAutoDetectChanges)
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var entityType in sortedEntityTypes)
        {
            await BulkInsertEntitiesOfTypeAsync(entityType, stats, ct);
        }
    }
    finally
    {
        if (DisableAutoDetectChanges)
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetect;
    }

    stats.EntityInsertionTime = entitySw.Elapsed;

    // Phase 3: Bulk insert junction table records
    var relationshipSw = Stopwatch.StartNew();
    await BulkInsertRelationshipsAsync(stats, ct);
    stats.RelationshipInsertionTime = relationshipSw.Elapsed;

    stats.TotalTime = totalSw.Elapsed;

    _logger?.LogInformation("Bulk insert completed in {Time:F2}s", stats.TotalTime.TotalSeconds);
    stats.LogSummary(_logger);

    return stats;
}

private List<Type> TopologicalSort()
{
    // Use EF Core metadata to determine dependency order
    var allTypes = _entityCaches.Keys.ToList();
    var sorted = new List<Type>();
    var visited = new HashSet<Type>();

    void Visit(Type type)
    {
        if (visited.Contains(type)) return;
        visited.Add(type);

        var entityType = _dbContext.Model.FindEntityType(type);
        var foreignKeys = entityType.GetForeignKeys();

        foreach (var fk in foreignKeys)
        {
            var principalType = fk.PrincipalEntityType.ClrType;
            if (allTypes.Contains(principalType))
            {
                Visit(principalType);
            }
        }

        sorted.Add(type);
    }

    foreach (var type in allTypes)
    {
        Visit(type);
    }

    return sorted;
}

private async Task BulkInsertEntitiesOfTypeAsync(
    Type entityType,
    BulkInsertStats stats,
    CancellationToken ct)
{
    var cache = _entityCaches[entityType];
    var newEntities = cache.GetNewEntities(); // Only entities marked as isNew

    if (newEntities.Count == 0)
        return;

    _logger?.LogInformation("Bulk inserting {Count:N0} {Type} entities...",
        newEntities.Count, entityType.Name);

    var sw = Stopwatch.StartNew();

    // Use EFCore.BulkExtensions
    await _dbContext.BulkInsertAsync(
        newEntities,
        new BulkConfig { SetOutputIdentity = SetOutputIdentity },
        cancellationToken: ct);

    stats.EntitiesInserted[entityType] = newEntities.Count;

    _logger?.LogInformation("✓ Inserted {Count:N0} {Type} in {Time:F2}s",
        newEntities.Count, entityType.Name, sw.Elapsed.TotalSeconds);
}

private async Task BulkInsertRelationshipsAsync(
    BulkInsertStats stats,
    CancellationToken ct)
{
    if (_relationships.Count == 0)
        return;

    // Group relationships by junction table
    var groupedRelationships = _relationships
        .GroupBy(r => r.JunctionInfo)
        .ToList();

    foreach (var group in groupedRelationships)
    {
        var junctionInfo = group.Key;
        var records = group
            .Select(r => (Left: r.LeftKey, Right: r.RightKey))
            .Distinct()
            .ToList();

        if (records.Count == 0)
            continue;

        _logger?.LogInformation("Bulk inserting {Count:N0} {Relationship} relationships...",
            records.Count,
            $"{junctionInfo.LeftType.Name} <-> {junctionInfo.RightType.Name}");

        await BulkInsertJunctionRecordsAsync(junctionInfo, records, ct);

        var relationshipKey = $"{junctionInfo.LeftType.Name} <-> {junctionInfo.RightType.Name}";
        stats.RelationshipsInserted[relationshipKey] = records.Count;
    }
}

private async Task BulkInsertJunctionRecordsAsync(
    JunctionTableInfo junctionInfo,
    List<(object Left, object Right)> records,
    CancellationToken ct)
{
    var chunks = records.Chunk(JunctionBatchSize);

    foreach (var chunk in chunks)
    {
        var leftIds = chunk.Select(r => r.Left).ToArray();
        var rightIds = chunk.Select(r => r.Right).ToArray();

        // PostgreSQL UNNEST with parameterized arrays
        var fullTableName = string.IsNullOrEmpty(junctionInfo.Schema)
            ? $"\"{junctionInfo.TableName}\""
            : $"\"{junctionInfo.Schema}\".\"{junctionInfo.TableName}\"";

        var sql = $@"
            INSERT INTO {fullTableName} (""{junctionInfo.LeftColumnName}"", ""{junctionInfo.RightColumnName}"")
            SELECT * FROM UNNEST(@leftIds::uuid[], @rightIds::uuid[])
            ON CONFLICT DO NOTHING";

        await _dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new[]
            {
                new NpgsqlParameter("@leftIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = leftIds },
                new NpgsqlParameter("@rightIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = rightIds }
            },
            ct);
    }
}
```

---

## Usage Example in IndexerService

### Before (Current Approach with Manual Caching):

```csharp
private class ScanContext
{
    private readonly Dictionary<string, Genre> _genreCache = new();

    public async Task<Genre> GetOrCreateGenre(string name)
    {
        if (_genreCache.TryGetValue(name, out var cached))
            return cached;

        var existing = await _context.Genres.FirstOrDefaultAsync(g => g.Name == name);
        if (existing != null)
        {
            _genreCache[name] = existing;
            return existing;
        }

        var genre = new Genre { Name = name };
        _genreCache[name] = genre;
        NewGenres.Add(genre);
        return genre;
    }
}

// Later: Manual bulk insert and junction table handling
await _context.BulkInsertAsync(ctx.NewGenres);
await _context.BulkInsertAsync(ctx.NewTracks);

var trackArtistJunctions = ctx.NewTracks
    .SelectMany(t => t.Artists.Select(a => (t.Id, a.Id)))
    .ToList();

await BulkJunctionHelper.BulkInsertJunctionAsync(_context, "TrackArtistWithRole", ...);
```

### After (With Bulk Insert Library):

```csharp
public async Task IndexLibraryAsync(MusicLibrary library)
{
    // Phase 1: Collect metadata (unchanged)
    var allAtlTracks = await CollectAllTracksAsync(library);

    // Phase 2 & 3: Process with bulk context
    var stats = await _context.BulkOperationAsync(async bulkContext =>
    {
        foreach (var atlTrack in allAtlTracks)
        {
            // Simple GetOrAdd - no manual cache management!
            var genre = !string.IsNullOrEmpty(atlTrack.Genre)
                ? await bulkContext.GetOrAddAsync(
                    key: atlTrack.Genre,
                    keySelector: g => g.Name,  // Genre uses Name property
                    createFunc: () => new Genre
                    {
                        Id = Guid.NewGuid(),
                        Name = atlTrack.Genre,
                        CreatedAt = DateTime.UtcNow
                    })
                : null;

            var label = !string.IsNullOrEmpty(atlTrack.Publisher)
                ? await bulkContext.GetOrAddAsync(
                    key: atlTrack.Publisher,
                    keySelector: l => l.Name,  // RecordLabel uses Name property
                    createFunc: () => new RecordLabel
                    {
                        Id = Guid.NewGuid(),
                        Name = atlTrack.Publisher
                    })
                : null;

            var artists = await ParseAndCreateArtists(atlTrack, bulkContext);

            var albumKey = AlbumKey.FromTrack(atlTrack);
            var album = await bulkContext.GetOrAddAsync(
                key: albumKey,
                keySelector: a => new { a.Name, a.ReleaseDate, a.DiscTotal, a.TrackTotal },  // Composite key
                createFunc: () => new Album
                {
                    Id = Guid.NewGuid(),
                    Name = albumKey.Name,
                    ReleaseDate = albumKey.ReleaseDate,
                    // ...
                });

            var track = await bulkContext.GetOrAddAsync(
                key: atlTrack.Path,
                keySelector: t => t.FilePath,  // Track uses FilePath property
                createFunc: () => new Track
                {
                    Id = Guid.NewGuid(),
                    Title = atlTrack.Title,
                    FilePath = atlTrack.Path,
                    // ...
                });

            // Register relationships - no manual junction table handling!
            foreach (var artist in artists)
            {
                bulkContext.RegisterRelationship(track, artist);
                bulkContext.RegisterRelationship(album, artist);
            }
        }

        // Explicitly save all changes
    }, new BulkInsertOptions { Logger = _logger });

    _logger.LogInformation("Indexed {Tracks} tracks in {Time:F2}s",
        stats.EntitiesInserted.GetValueOrDefault(typeof(Track)),
        stats.TotalTime.TotalSeconds);
}

private async Task<List<ArtistWithRole>> ParseAndCreateArtists(
    ATL.Track atlTrack,
    BulkInsertContext bulkContext)
{
    var artistNames = ParseArtistString(atlTrack.Artist);
    var results = new List<ArtistWithRole>();

    foreach (var (name, role) in artistNames)
    {
        var artist = await bulkContext.GetOrAddAsync(
            key: name,
            keySelector: a => a.Name,
            createFunc: () => new Artist
            {
                Id = Guid.NewGuid(),
                Name = name
            });

        // For ArtistWithRole, we need a composite key (ArtistId + Role)
        var artistWithRole = await bulkContext.GetOrAddAsync(
            key: (artist.Id, role),
            keySelector: awr => new { awr.ArtistId, awr.Role },
            createFunc: () => new ArtistWithRole
            {
                Id = Guid.NewGuid(),
                ArtistId = artist.Id,
                Role = role
            });

        results.Add(artistWithRole);
    }

    return results;
}
```

**Keyword Indexing Example:**
```csharp
// Phase 4b: Bulk keyword indexing with the library
foreach (var track in tracksNeedingKeywords)
{
    var keywordValues = ProcessInputString(track.ToString()); // From SearchService

    foreach (var value in keywordValues)
    {
        var keyword = await bulkContext.GetOrAddAsync(
            key: value,
            keySelector: k => k.Value,  // Keyword uses Value property, not Name
            createFunc: () => new Keyword
            {
                Id = Guid.NewGuid(),
                Value = value
            });

        bulkContext.RegisterRelationship(keyword, track);
    }
}
```

**Key Improvements:**
1. ✅ No manual cache management - `GetOrAddAsync` handles it
2. ✅ No manual junction table SQL - `RegisterRelationship` handles it
3. ✅ No manual dependency ordering - `SaveChangesAsync` handles it
4. ✅ Clean, readable code that focuses on business logic
5. ✅ Automatic performance optimizations (bulk inserts, chunking, etc.)
6. ✅ Flexible key selectors - different entities use different properties (Name vs Value)

---

## Internal Data Structures

```csharp
internal class EntityCache<TEntity> where TEntity : class
{
    private readonly Dictionary<object, CachedEntity<TEntity>> _cache = new();

    public bool TryGetValue(object key, out CachedEntity<TEntity> cached)
        => _cache.TryGetValue(key, out cached);

    public CachedEntity<TEntity> this[object key]
    {
        get => _cache[key];
        set => _cache[key] = value;
    }

    public bool ContainsEntity(TEntity entity)
        => _cache.Values.Any(c => ReferenceEquals(c.Entity, entity));

    public List<TEntity> GetNewEntities()
        => _cache.Values.Where(c => c.IsNew).Select(c => c.Entity).ToList();

    public List<TEntity> GetAllEntities()
        => _cache.Values.Select(c => c.Entity).ToList();
}

internal record CachedEntity<TEntity>(TEntity Entity, bool IsNew);

internal record JunctionTableInfo(
    string TableName,
    string? Schema,
    string LeftColumnName,
    string RightColumnName,
    Type LeftType,
    Type RightType);

internal record RelationshipRegistration(
    JunctionTableInfo JunctionInfo,
    object LeftKey,
    object RightKey);
```

---

## Advanced Features (Future Enhancements)

### 1. Update Support

```csharp
public async Task<TEntity> GetOrUpdateAsync<TEntity>(
    object key,
    Expression<Func<TEntity, object>> keySelector,
    Func<TEntity> createFunc,
    Action<TEntity>? updateFunc = null)
    where TEntity : class
{
    var entity = await GetOrAddAsync(key, keySelector, createFunc);

    if (updateFunc != null && !IsNew(entity))
    {
        updateFunc(entity);
        MarkForUpdate(entity);
    }

    return entity;
}
```

### 2. Batch Query Optimization

Instead of querying one entity at a time in `GetOrAddAsync`, collect all keys and batch the queries:

```csharp
public async Task<TEntity> GetOrAddDeferred<TEntity>(
    object key,
    Expression<Func<TEntity, object>> keySelector,
    Func<TEntity> createFunc)
{
    // Add to deferred query queue
    // Resolve in batches when SaveChangesAsync is called
    // Example: WHERE Name IN (@name1, @name2, @name3, ...)
}
```

### 3. Multi-Database Support

Detect database provider and use appropriate SQL:
- PostgreSQL: `UNNEST(@array1::uuid[], @array2::uuid[])`
- SQL Server: `INSERT ... SELECT * FROM OPENJSON(@json)`
- MySQL: `INSERT ... VALUES (?, ?), (?, ?), ...`

---

## Project Structure

```
src/Coral.BulkInsert/
├── BulkInsertContext.cs          # Main context class
├── BulkInsertExtensions.cs       # DbContext extension methods
├── BulkInsertOptions.cs          # Configuration options
├── BulkInsertStats.cs            # Statistics and diagnostics
├── Internal/
│   ├── EntityCache.cs            # Generic entity cache
│   ├── JunctionTableInfo.cs      # Junction table metadata
│   ├── RelationshipRegistration.cs
│   ├── TopologicalSorter.cs      # Dependency ordering
│   └── DatabaseProviders/
│       ├── IDbProvider.cs
│       ├── PostgreSqlProvider.cs # PostgreSQL-specific SQL
│       └── SqlServerProvider.cs  # Future: SQL Server support
└── Coral.BulkInsert.csproj

tests/Coral.BulkInsert.Tests/
├── BulkInsertContextTests.cs
├── GetOrAddTests.cs
├── RegisterRelationshipTests.cs
├── TopologicalSortTests.cs
└── IntegrationTests/
    └── PostgreSqlBulkInsertTests.cs  # Testcontainers
```

---

## Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />
    <PackageReference Include="EFCore.BulkExtensions" Version="9.0.0" />
    <PackageReference Include="Npgsql" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  </ItemGroup>
</Project>
```

---

## Design Decisions & Notes

1. **✅ Explicit Save Required**
   - `SaveChangesAsync()` must be called explicitly for predictability
   - No auto-save on dispose to avoid surprises

2. **✅ Composite Keys via Key Selector**
   - Not supporting composite primary keys (Coral uses Guid IDs)
   - Key selector supports composite lookup keys (e.g., `new { Name, ReleaseDate }`)
   - This covers Album deduplication and ArtistWithRole (ArtistId + Role)

3. **Update vs Insert Detection**
   - Current design: `GetOrAddAsync` queries DB to detect existing entities
   - Returns existing entity if found, creates new if not found
   - Updates not supported in v1 - entities are either new or existing (no modification)

4. **Relationship Direction**
   - `RegisterRelationship(track, artist)` vs `RegisterRelationship(artist, track)` should be treated as equivalent
   - Implementation will normalize by checking EF metadata for junction table structure
   - Always stores relationships in consistent order internally

5. **Thread Safety**
   - BulkInsertContext wraps DbContext (not thread-safe)
   - Document as single-threaded use only
   - For parallel operations (Phase 4 artwork), use separate contexts per thread

---

## Migration Path

### Phase 1: Library Development
1. Create `Coral.BulkInsert` project
2. Implement core classes (BulkInsertContext, EntityCache, etc.)
3. Write unit tests for GetOrAdd, RegisterRelationship
4. Write integration tests with Testcontainers

### Phase 2: IndexerService Integration
1. Update IndexerService to use BulkInsertContext
2. Remove manual ScanContext and BulkJunctionHelper
3. Benchmark against current implementation

### Phase 3: Refinement
1. Add logging and diagnostics
2. Optimize batch query strategies
3. Add support for other database providers (SQL Server, MySQL)

---

## Success Criteria

✅ **API Simplicity:** IndexerService code is cleaner and more readable
✅ **Performance:** Matches or exceeds current bulk insert performance (~110s for 7k tracks)
✅ **Maintainability:** No manual junction table SQL in business logic
✅ **Type Safety:** Generic API with compile-time type checking
✅ **Testability:** Comprehensive unit and integration tests
✅ **Reusability:** Generic enough to use across all Coral services

---

## Next Steps

1. Review and approve API design
2. Iterate on any concerns or suggestions
3. Begin implementation in `Coral.BulkInsert` project
4. Create unit tests for core functionality
5. Integration test with Testcontainers + PostgreSQL
6. Integrate into IndexerService and benchmark

