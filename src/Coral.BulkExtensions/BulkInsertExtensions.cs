using System.Linq.Expressions;
using Coral.BulkExtensions.Internal;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Coral.BulkExtensions;

/// <summary>
/// EF Core-style extension methods for bulk operations.
/// </summary>
public static class BulkInsertExtensions
{
    /// <summary>
    /// Gets an existing entity from cache/DB or adds a new one to the bulk context.
    /// Similar to FirstOrDefault() + Add() pattern but with automatic caching.
    /// </summary>
    public static async Task<TEntity> GetOrAddBulk<TEntity>(
        this DbContext context,
        Expression<Func<TEntity, object>> keySelector,
        Func<TEntity> createFunc)
        where TEntity : BaseTable
    {
        var bulkContext = BulkContextStorage.GetOrCreate(context);
        return await bulkContext.GetOrAddAsync(keySelector, createFunc);
    }

    /// <summary>
    /// Registers a many-to-many relationship between two entities.
    /// Junction table resolution is automatic via EF Core metadata.
    /// </summary>
    public static void AddRelationshipBulk<TLeft, TRight>(
        this DbContext context,
        TLeft left,
        TRight right)
        where TLeft : BaseTable
        where TRight : BaseTable
    {
        var bulkContext = BulkContextStorage.GetOrCreate(context);
        bulkContext.RegisterRelationship(left, right);
    }

    /// <summary>
    /// Bulk saves all pending operations and returns statistics.
    /// This is the explicit save operation - nothing is saved automatically.
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="options">Options for bulk insert operations</param>
    /// <param name="retainCache">If true, keeps the entity cache after save for registering relationships. Default: false.</param>
    /// <param name="ct">Cancellation token</param>
    public static async Task<BulkInsertStats> SaveBulkChangesAsync(
        this DbContext context,
        BulkInsertOptions? options = null,
        bool retainCache = false,
        CancellationToken ct = default)
    {
        var bulkContext = BulkContextStorage.GetOrCreate(context, options);
        var stats = await bulkContext.SaveChangesAsync(ct);

        // Clear after save to release memory (unless retaining for relationships)
        if (!retainCache)
        {
            BulkContextStorage.Clear(context);
        }

        return stats;
    }
}
