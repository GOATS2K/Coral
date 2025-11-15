using System.Linq.Expressions;
using Coral.BulkExtensions.Internal;
using Coral.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coral.BulkExtensions;

/// <summary>
/// Convenience extension methods on DbSet&lt;T&gt; for bulk operations.
/// </summary>
public static class DbSetBulkExtensions
{
    /// <summary>
    /// Gets an existing entity from cache/DB or adds a new one to the bulk context.
    /// Convenience method that automatically infers the entity type from the DbSet.
    /// </summary>
    /// <example>
    /// var genre = await context.Genres.GetOrAddBulk(
    ///     g => g.Name,
    ///     () => new Genre { Id = Guid.NewGuid(), Name = "Rock" });
    /// </example>
    public static async Task<TEntity> GetOrAddBulk<TEntity>(
        this DbSet<TEntity> dbSet,
        Expression<Func<TEntity, object>> keySelector,
        Func<TEntity> createFunc)
        where TEntity : BaseTable
    {
        var context = dbSet.GetContext();
        var bulkContext = BulkContextStorage.GetOrCreate(context);
        return await bulkContext.GetOrAddAsync(keySelector, createFunc);
    }

    /// <summary>
    /// Helper method to get DbContext from DbSet.
    /// Uses the EntityType property to access the DbContext via model.
    /// </summary>
    private static DbContext GetContext<TEntity>(this DbSet<TEntity> dbSet)
        where TEntity : class
    {
        // Access the internal IInfrastructure<IServiceProvider> interface
        var infrastructure = dbSet as Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>;
        if (infrastructure == null)
            throw new InvalidOperationException("Unable to access DbSet infrastructure");

        var serviceProvider = infrastructure.Instance;
        var currentDbContext = serviceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.Infrastructure.ICurrentDbContext))
            as Microsoft.EntityFrameworkCore.Infrastructure.ICurrentDbContext;

        if (currentDbContext?.Context == null)
            throw new InvalidOperationException("Unable to get DbContext from DbSet");

        return currentDbContext.Context;
    }
}
