using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Coral.BulkExtensions.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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

    // Track if SQLite optimizations have been applied
    private bool _sqliteOptimized = false;

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

        // Mark all inserted entities as no longer new (important for retainCache scenarios)
        MarkEntitiesAsSaved();

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

        // SQLite bulk insert
        await BulkInsertWithSqliteAsync(entityType, newEntities, ct);

        stats.EntitiesInserted[entityType] = newEntities.Count;

        _logger?.LogInformation("✓ Inserted {Count:N0} {Type} in {Time:F2}s ({Rate:N0} entities/sec)",
            newEntities.Count, entityType.Name, sw.Elapsed.TotalSeconds,
            newEntities.Count / sw.Elapsed.TotalSeconds);
    }

    private async Task OptimizeSqliteForBulkInsert(System.Data.Common.DbConnection connection)
    {
        // Only optimize once, and not if we're inside a transaction
        if (_sqliteOptimized)
            return;

        // Check if we're in a transaction - if so, skip optimizations
        // (SQLite doesn't allow PRAGMA changes inside transactions)
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode";
            var currentMode = await command.ExecuteScalarAsync();

            // If we can read the pragma, we're not in a restrictive transaction state
            // Apply optimizations
            await ExecutePragmaAsync(connection, "PRAGMA journal_mode = WAL");
            await ExecutePragmaAsync(connection, "PRAGMA synchronous = NORMAL");
            await ExecutePragmaAsync(connection, "PRAGMA cache_size = -64000");
            await ExecutePragmaAsync(connection, "PRAGMA temp_store = MEMORY");

            _sqliteOptimized = true;
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Error code 1 = "Safety level may not be changed inside a transaction"
            // This is expected in test scenarios - just continue without optimization
            _sqliteOptimized = true; // Don't try again
        }
    }

    private async Task ExecutePragmaAsync(System.Data.Common.DbConnection connection, string pragma)
    {
        using var command = connection.CreateCommand();
        command.CommandText = pragma;
        await command.ExecuteNonQueryAsync();
    }

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

        // Get scalar properties (columns)
        var properties = efEntityType.GetProperties()
            .Where(p => !p.IsShadowProperty() && p.GetColumnName() != null)
            .ToList();

        // Get owned navigations that are mapped to JSON columns
        var jsonNavigations = efEntityType.GetNavigations()
            .Where(n => n.ForeignKey.IsOwnership && n.TargetEntityType.IsMappedToJson())
            .ToList();

        var columnNames = properties.Select(p => p.GetColumnName()).ToList();

        // Add JSON column names for owned navigations
        foreach (var nav in jsonNavigations)
        {
            var columnName = nav.TargetEntityType.GetContainerColumnName();
            if (columnName != null)
            {
                columnNames.Add(columnName);
            }
        }
        var paramPlaceholders = string.Join(", ",
            Enumerable.Range(0, columnNames.Count).Select(i => $"@p{i}"));

        var columnNamesStr = string.Join(", ", columnNames.Select(c => $"\"{c}\""));
        var insertSql = $"INSERT INTO \"{tableName}\" ({columnNamesStr}) VALUES ({paramPlaceholders})";

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        // Apply SQLite optimizations once
        await OptimizeSqliteForBulkInsert(connection);

        // Use ADO.NET for optimal performance
        using var command = connection.CreateCommand();
        command.CommandText = insertSql;

        // Create parameters once and reuse (critical for performance)
        var totalParams = properties.Count + jsonNavigations.Count;
        var parameters = Enumerable.Range(0, totalParams).Select(i =>
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
                // Set parameter values for scalar properties
                for (int i = 0; i < properties.Count; i++)
                {
                    var property = properties[i];
                    var value = GetPropertyValue(entity, property);
                    parameters[i].Value = value ?? DBNull.Value;
                }

                // Set parameter values for JSON navigations
                for (int i = 0; i < jsonNavigations.Count; i++)
                {
                    var navigation = jsonNavigations[i];
                    var value = GetNavigationPropertyValue(entity, navigation);
                    parameters[properties.Count + i].Value = value ?? DBNull.Value;
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

        // Apply EF Core value converter if one exists (e.g., string[] -> string)
        var converter = property.GetValueConverter();
        if (converter != null && value != null)
        {
            value = converter.ConvertToProvider(value);
        }

        // Handle enums - convert to underlying integer type
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (underlyingType.IsEnum && value != null)
        {
            value = Convert.ToInt32(value);
        }

        return value;
    }

    private object? GetNavigationPropertyValue(object entity, Microsoft.EntityFrameworkCore.Metadata.INavigation navigation)
    {
        var value = navigation.PropertyInfo?.GetValue(entity);

        // For JSON-configured owned navigations, serialize to JSON
        if (navigation.TargetEntityType.IsMappedToJson() && value != null)
        {
            value = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = null, // Use exact property names
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });
        }

        return value;
    }

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

    private async Task BulkInsertJunctionRecordsSqliteAsync(
        JunctionTableInfo junctionInfo,
        List<(Guid Left, Guid Right)> records,
        CancellationToken ct)
    {
        var fullTableName = $"\"{junctionInfo.TableName}\"";

        // SQLite uses INSERT OR IGNORE instead of ON CONFLICT DO NOTHING
        var sql = $"INSERT OR IGNORE INTO {fullTableName} (\"{junctionInfo.LeftColumnName}\", \"{junctionInfo.RightColumnName}\") VALUES (@left, @right)";

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

    /// <summary>
    /// Marks all entities that were inserted as no longer new.
    /// This is important for retainCache scenarios where the cache persists across saves.
    /// </summary>
    private void MarkEntitiesAsSaved()
    {
        foreach (var cache in _entityCaches.Values.Cast<IEntityCache>())
        {
            cache.MarkAllAsExisting();
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
