using Application.Contracts.Caching;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Persistence.Repositories.Base;

/// <summary>
/// Base helpers for read-side repositories: optional distributed cache with consistent key naming.
/// After any write that affects read models, call <see cref="RemoveCacheKeysAsync"/> on the write side or domain-specific invalidation.
/// </summary>
public abstract class CachedReadRepositoryBase : RepositoryCacheBase
{
    protected CachedReadRepositoryBase(ICacheService cache) : base(cache)
    {
    }

    /// <summary>Single-entity load with optional cache (default on).</summary>
    protected async Task<T?> GetSingleCachedAsync<T>(
        string cacheKey,
        Func<Task<T?>> loadFromDb,
        CacheOptions options,
        TimeSpan defaultTtl,
        CancellationToken cancellationToken) where T : class =>
        await Cache.GetOrSetAsync(cacheKey, loadFromDb, options.TimeToLive ?? defaultTtl, options.Enabled, cancellationToken);

    /// <summary>Runs a query without reading or writing the cache (counts, exists checks, projections).</summary>
    protected static Task<TResult> QueryAsync<TResult>(Func<Task<TResult>> query) => query();

    /// <summary>Runs an uncached load when caching is not appropriate for this path.</summary>
    protected static Task<T?> LoadUncachedAsync<T>(Func<Task<T?>> loadFromDb) where T : class => loadFromDb();

    /// <summary>Reusable EF read helper for single-entity lookup by predicate (no tracking).</summary>
    protected static Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class =>
        source.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

    /// <summary>Reusable EF read helper for existence checks (no tracking).</summary>
    protected static Task<bool> ExistsAsync<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class =>
        source.AsNoTracking().AnyAsync(predicate, cancellationToken);

    /// <summary>Reusable cached lookup by predicate with optional cache toggle and TTL override.</summary>
    protected Task<TEntity?> GetSingleCachedByPredicateAsync<TEntity>(
        string cacheKey,
        IQueryable<TEntity> source,
        Expression<Func<TEntity, bool>> predicate,
        CacheOptions options,
        TimeSpan defaultTtl,
        CancellationToken cancellationToken = default) where TEntity : class =>
        GetSingleCachedAsync(
            cacheKey,
            () => FirstOrDefaultAsync(source, predicate, cancellationToken),
            options,
            defaultTtl,
            cancellationToken);
}
