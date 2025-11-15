using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Coral.BulkExtensions.Internal;

/// <summary>
/// Manages BulkInsertContext instances per DbContext.
/// Uses ConditionalWeakTable to avoid preventing DbContext garbage collection.
/// </summary>
internal static class BulkContextStorage
{
    private static readonly ConditionalWeakTable<DbContext, BulkInsertContext> _contexts = new();

    public static BulkInsertContext GetOrCreate(DbContext dbContext, BulkInsertOptions? options = null)
    {
        return _contexts.GetValue(dbContext, _ => new BulkInsertContext(dbContext, options));
    }

    public static void Clear(DbContext dbContext)
    {
        if (_contexts.TryGetValue(dbContext, out var bulkContext))
        {
            bulkContext.Clear();
            _contexts.Remove(dbContext);
        }
    }
}
