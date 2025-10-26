using System.Linq.Expressions;
using Coral.BulkExtensions.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;

namespace Coral.BulkExtensions;

/// <summary>
/// Context for batching entity operations and relationships for bulk insertion.
/// Wraps a DbContext and provides caching, relationship tracking, and bulk save operations.
/// </summary>
public sealed class BulkInsertContext : IAsyncDisposable
{
    private readonly DbContext _dbContext;
    private readonly BulkInsertOptions _options;
    private readonly ILogger? _logger;
    private readonly Dictionary<Type, object> _entityCaches = new();
    private readonly HashSet<RelationshipRegistration> _relationships = new();

    // Metadata caches for performance
    private readonly Dictionary<(Type, Type), JunctionTableInfo> _junctionTableCache = new();
    private readonly Dictionary<string, Delegate> _compiledSelectorCache = new();

    internal BulkInsertContext(DbContext dbContext, BulkInsertOptions? options = null)
    {
        _dbContext = dbContext;
        _options = options ?? new BulkInsertOptions();
        _logger = _options.Logger;
    }

    /// <summary>
    /// Gets an existing entity from cache/DB or adds a new one.
    /// Similar to ConcurrentDictionary.GetOrAdd() semantics.
    /// </summary>
    /// <param name="keySelector">Expression to extract the key from an entity (e.g., g => g.Name)</param>
    /// <param name="createFunc">Function to create a new entity if not found. The created entity should have all properties set including Id.</param>
    public async Task<TEntity> GetOrAddAsync<TEntity>(
        Expression<Func<TEntity, object>> keySelector,
        Func<TEntity> createFunc)
        where TEntity : class
    {
        // Create the entity to extract the key from
        var templateEntity = createFunc();

        // Get or create typed cache for this entity type
        var cache = GetOrCreateCache<TEntity>();

        // Compile the key selector (with caching) and extract the key value from the template
        var selectorKey = keySelector.ToString();
        if (!_compiledSelectorCache.TryGetValue(selectorKey, out var compiledDelegate))
        {
            compiledDelegate = keySelector.Compile();
            _compiledSelectorCache[selectorKey] = compiledDelegate;
        }
        var compiledSelector = (Func<TEntity, object>)compiledDelegate;
        var keyValue = compiledSelector(templateEntity);

        // Create a cache key that includes the selector to support multiple lookup strategies
        var cacheKey = (selectorKey, keyValue);

        // Check cache first
        if (cache.TryGetValue(cacheKey, out var cached) && cached != null)
        {
            return cached.Entity;
        }

        // Query database using the provided selector (only once per unique key)
        var existing = await QueryEntityByKeySelectorAsync(keyValue, keySelector);
        if (existing != null)
        {
            cache[cacheKey] = new CachedEntity<TEntity>(existing, IsNew: false);
            return existing;
        }

        // Use the template entity we already created
        cache[cacheKey] = new CachedEntity<TEntity>(templateEntity, IsNew: true);

        return templateEntity;
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
        object keyValue,
        Expression<Func<TEntity, object>> keySelector)
        where TEntity : class
    {
        var dbSet = _dbContext.Set<TEntity>();

        // Build Where(e => keySelector(e) == keyValue)
        var parameter = keySelector.Parameters[0];
        var body = keySelector.Body;

        // Handle boxing conversion if present (when selector returns value type)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            body = unary.Operand;
        }

        Expression predicate;

        // For composite keys (anonymous types), we need to decompose into individual property comparisons
        // EF Core cannot translate anonymous type equality directly
        if (body is NewExpression newExpr)
        {
            // Build: e.Prop1 == keyValue.Prop1 && e.Prop2 == keyValue.Prop2 && ...
            var keyType = keyValue.GetType();
            var keyProperties = keyType.GetProperties();

            Expression? combined = null;

            for (int i = 0; i < newExpr.Arguments.Count; i++)
            {
                var memberExpr = newExpr.Arguments[i];
                var keyProperty = keyProperties[i];
                var propertyValue = keyProperty.GetValue(keyValue);

                var constant = Expression.Constant(propertyValue, memberExpr.Type);
                var equals = Expression.Equal(memberExpr, constant);

                combined = combined == null ? equals : Expression.AndAlso(combined, equals);
            }

            predicate = combined!;
        }
        else
        {
            // Simple property comparison
            var constant = Expression.Constant(keyValue, body.Type);
            predicate = Expression.Equal(body, constant);
        }

        var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);

        return await dbSet.AsNoTracking().FirstOrDefaultAsync(lambda);
    }

    /// <summary>
    /// Registers a many-to-many relationship between two entities.
    /// Junction table info is resolved automatically from EF Core metadata.
    /// </summary>
    public void RegisterRelationship<TLeft, TRight>(TLeft left, TRight right)
        where TLeft : Database.Models.BaseTable
        where TRight : Database.Models.BaseTable
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

        // Store relationship for bulk insert (HashSet automatically deduplicates)
        _relationships.Add(new RelationshipRegistration(junctionInfo, leftKey, rightKey));
    }

    private JunctionTableInfo ResolveJunctionTable<TLeft, TRight>()
    {
        var typePair = (typeof(TLeft), typeof(TRight));

        // Check cache first
        if (_junctionTableCache.TryGetValue(typePair, out var cachedInfo))
            return cachedInfo;

        var leftType = _dbContext.Model.FindEntityType(typeof(TLeft));
        var rightType = _dbContext.Model.FindEntityType(typeof(TRight));

        if (leftType == null)
            throw new InvalidOperationException($"Entity type {typeof(TLeft).Name} not found in DbContext model");
        if (rightType == null)
            throw new InvalidOperationException($"Entity type {typeof(TRight).Name} not found in DbContext model");

        // Find the skip navigation (many-to-many relationship)
        var skipNavigation = leftType.GetSkipNavigations()
            .FirstOrDefault(n => n.TargetEntityType == rightType);

        if (skipNavigation == null)
        {
            // Try the reverse direction
            skipNavigation = rightType.GetSkipNavigations()
                .FirstOrDefault(n => n.TargetEntityType == leftType);

            if (skipNavigation == null)
                throw new InvalidOperationException(
                    $"No many-to-many relationship found between {typeof(TLeft).Name} and {typeof(TRight).Name}");
        }

        // Extract junction table metadata
        var joinEntityType = skipNavigation.JoinEntityType;
        if (joinEntityType == null)
            throw new InvalidOperationException(
                $"Junction entity type not found for relationship between {typeof(TLeft).Name} and {typeof(TRight).Name}");

        var tableName = joinEntityType.GetTableName();
        var schema = joinEntityType.GetSchema();

        // Get foreign key column names
        var leftForeignKey = joinEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == leftType);
        var rightForeignKey = joinEntityType.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == rightType);

        if (leftForeignKey == null || rightForeignKey == null)
            throw new InvalidOperationException(
                $"Could not resolve foreign keys for junction table between {typeof(TLeft).Name} and {typeof(TRight).Name}");

        var leftColumnName = leftForeignKey.Properties[0].GetColumnName();
        var rightColumnName = rightForeignKey.Properties[0].GetColumnName();

        var junctionInfo = new JunctionTableInfo(
            TableName: tableName!,
            Schema: schema,
            LeftColumnName: leftColumnName!,
            RightColumnName: rightColumnName!,
            LeftType: typeof(TLeft),
            RightType: typeof(TRight)
        );

        // Cache for future lookups
        _junctionTableCache[typePair] = junctionInfo;

        return junctionInfo;
    }

    private static Guid GetPrimaryKey<TEntity>(TEntity entity) where TEntity : Database.Models.BaseTable
    {
        if (entity.Id == Guid.Empty)
            throw new InvalidOperationException($"Entity {typeof(TEntity).Name} has empty Id");

        return entity.Id;
    }

    /// <summary>
    /// Bulk saves all cached entities and relationships.
    /// Handles dependency ordering and junction table insertion automatically.
    /// </summary>
    public async Task<BulkInsertStats> SaveChangesAsync(CancellationToken ct = default)
    {
        var stats = new BulkInsertStats();
        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        // Phase 1: Sort entities by dependency order
        var sortedEntityTypes = TopologicalSort();

        _logger?.LogInformation("Bulk inserting {Count} entity types in dependency order",
            sortedEntityTypes.Count);

        // Phase 2: Bulk insert entities in dependency order
        var entitySw = System.Diagnostics.Stopwatch.StartNew();

        var originalAutoDetect = _dbContext.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            if (_options.DisableAutoDetectChanges)
                _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            foreach (var entityType in sortedEntityTypes)
            {
                await BulkInsertEntitiesOfTypeAsync(entityType, stats, ct);
            }
        }
        finally
        {
            if (_options.DisableAutoDetectChanges)
                _dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetect;
        }

        stats.EntityInsertionTime = entitySw.Elapsed;

        // Phase 3: Bulk insert junction table records
        var relationshipSw = System.Diagnostics.Stopwatch.StartNew();
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
        var visiting = new HashSet<Type>();

        void Visit(Type type)
        {
            if (visited.Contains(type)) return;

            if (visiting.Contains(type))
                throw new InvalidOperationException($"Circular dependency detected involving {type.Name}");

            visiting.Add(type);

            var entityType = _dbContext.Model.FindEntityType(type);
            if (entityType != null)
            {
                var foreignKeys = entityType.GetForeignKeys();

                foreach (var fk in foreignKeys)
                {
                    var principalType = fk.PrincipalEntityType.ClrType;
                    if (allTypes.Contains(principalType))
                    {
                        Visit(principalType);
                    }
                }
            }

            visiting.Remove(type);
            visited.Add(type);
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
        var cache = (IEntityCache)_entityCaches[entityType];
        var newEntities = cache.GetNewEntitiesUntyped();

        if (newEntities.Count == 0)
            return;

        _logger?.LogInformation("Bulk inserting {Count:N0} {Type} entities...",
            newEntities.Count, entityType.Name);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Insert entities in batches using raw SQL
        await BulkInsertWithPostgresAsync(entityType, newEntities, ct);

        stats.EntitiesInserted[entityType] = newEntities.Count;

        _logger?.LogInformation("✓ Inserted {Count:N0} {Type} in {Time:F2}s ({Rate:N0} entities/sec)",
            newEntities.Count, entityType.Name, sw.Elapsed.TotalSeconds,
            newEntities.Count / sw.Elapsed.TotalSeconds);
    }

    private async Task BulkInsertWithPostgresAsync(
        Type entityType,
        System.Collections.IList entities,
        CancellationToken ct)
    {
        if (entities.Count == 0)
            return;

        // Get entity metadata from EF Core
        var efEntityType = _dbContext.Model.FindEntityType(entityType);
        if (efEntityType == null)
            throw new InvalidOperationException($"Entity type {entityType.Name} not found in DbContext model");

        var tableName = efEntityType.GetTableName();
        var schema = efEntityType.GetSchema();
        var fullTableName = string.IsNullOrEmpty(schema)
            ? $"\"{tableName}\""
            : $"\"{schema}\".\"{tableName}\"";

        // Get all properties that should be inserted (excluding navigation properties)
        var properties = efEntityType.GetProperties()
            .Where(p => !p.IsShadowProperty() && p.GetColumnName() != null)
            .ToList();

        // Get connection from DbContext
        var connection = (Npgsql.NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        // Use PostgreSQL COPY for bulk insert
        var columnNames = properties.Select(p => p.GetColumnName()).ToList();
        var copyCommand = $"COPY {fullTableName} ({string.Join(", ", columnNames.Select(c => $"\"{c}\""))}) FROM STDIN (FORMAT BINARY)";

        // Process entities in batches to manage memory
        var entityBatches = entities.Cast<object>().Chunk(_options.EntityBatchSize);

        foreach (var batch in entityBatches)
        {
            await using var writer = await connection.BeginBinaryImportAsync(copyCommand, ct);

            foreach (var entity in batch)
            {
                await writer.StartRowAsync(ct);

                foreach (var property in properties)
                {
                    var value = property.PropertyInfo?.GetValue(entity);
                    var clrType = property.ClrType;

                    // If this is a foreign key property and it's null, try to get the value from the navigation property
                    if (value == null || (value is Guid guidValue && guidValue == Guid.Empty))
                    {
                        var foreignKeys = property.GetContainingForeignKeys();
                        if (foreignKeys.Any())
                        {
                            var foreignKey = foreignKeys.First();
                            var navigation = foreignKey.DependentToPrincipal;

                            if (navigation != null && navigation.PropertyInfo != null)
                            {
                                var navigationValue = navigation.PropertyInfo.GetValue(entity);
                                if (navigationValue != null)
                                {
                                    // Get the principal key value from the related entity (always Id for BaseTable)
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

                    // Map to NpgsqlDbType and write
                    var dbType = MapClrToNpgsqlType(underlyingType.IsEnum ? typeof(int) : clrType);
                    await writer.WriteAsync(value, dbType, ct);
                }
            }

            await writer.CompleteAsync(ct);
        }
    }

    private static readonly Dictionary<Type, NpgsqlDbType> _typeToNpgsqlTypeCache = new()
    {
        [typeof(Guid)] = NpgsqlDbType.Uuid,
        [typeof(string)] = NpgsqlDbType.Text,
        [typeof(int)] = NpgsqlDbType.Integer,
        [typeof(long)] = NpgsqlDbType.Bigint,
        [typeof(DateTime)] = NpgsqlDbType.TimestampTz,
        [typeof(bool)] = NpgsqlDbType.Boolean,
        [typeof(decimal)] = NpgsqlDbType.Numeric,
        [typeof(double)] = NpgsqlDbType.Double,
        [typeof(float)] = NpgsqlDbType.Real
    };

    private static NpgsqlDbType MapClrToNpgsqlType(Type clrType)
    {
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return _typeToNpgsqlTypeCache.TryGetValue(underlyingType, out var npgsqlType)
            ? npgsqlType
            : NpgsqlDbType.Text; // Fallback
    }

    private async Task BulkInsertRelationshipsAsync(
        BulkInsertStats stats,
        CancellationToken ct)
    {
        if (_relationships.Count == 0)
            return;

        // Group relationships by junction table (already deduplicated by HashSet)
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

            await BulkInsertJunctionRecordsAsync(junctionInfo, records, ct);

            var relationshipKey = $"{junctionInfo.LeftType.Name} <-> {junctionInfo.RightType.Name}";
            stats.RelationshipsInserted[relationshipKey] = records.Count;

            _logger?.LogInformation("✓ Inserted {Count:N0} relationships in {Time:F2}s ({Rate:N0} records/sec)",
                records.Count, sw.Elapsed.TotalSeconds,
                records.Count / sw.Elapsed.TotalSeconds);
        }
    }

    private async Task BulkInsertJunctionRecordsAsync(
        JunctionTableInfo junctionInfo,
        List<(Guid Left, Guid Right)> records,
        CancellationToken ct)
    {
        var chunks = records.Chunk(_options.JunctionBatchSize);

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
                    new Npgsql.NpgsqlParameter("@leftIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = leftIds },
                    new Npgsql.NpgsqlParameter("@rightIds", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = rightIds }
                },
                ct);
        }
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
        // No auto-save - require explicit SaveChangesAsync
        await Task.CompletedTask;
    }
}
