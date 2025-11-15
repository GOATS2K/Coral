namespace Coral.BulkExtensions.Internal;

internal class EntityCache<TEntity> : IEntityCache where TEntity : class
{
    private readonly Dictionary<object, CachedEntity<TEntity>> _cache = new();
    private readonly HashSet<TEntity> _entitySet = new(ReferenceEqualityComparer.Instance);

    public bool TryGetValue(object key, out CachedEntity<TEntity>? cached)
        => _cache.TryGetValue(key, out cached);

    public CachedEntity<TEntity> this[object key]
    {
        get => _cache[key];
        set
        {
            _cache[key] = value;
            _entitySet.Add(value.Entity);
        }
    }

    public bool ContainsEntity(TEntity entity)
        => _entitySet.Contains(entity);

    public List<TEntity> GetNewEntities()
        => _cache.Values.Where(c => c.IsNew).Select(c => c.Entity).ToList();

    public List<TEntity> GetAllEntities()
        => _cache.Values.Select(c => c.Entity).ToList();

    public System.Collections.IList GetNewEntitiesUntyped()
        => GetNewEntities();

    public void MarkAllAsExisting()
    {
        // Update all cached entities to mark them as no longer new
        var keys = _cache.Keys.ToList();
        foreach (var key in keys)
        {
            var cached = _cache[key];
            _cache[key] = new CachedEntity<TEntity>(cached.Entity, IsNew: false);
        }
    }
}
