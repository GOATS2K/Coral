namespace Coral.BulkExtensions.Internal;

internal record CachedEntity<TEntity>(TEntity Entity, bool IsNew);
