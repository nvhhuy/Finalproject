using System.Linq.Expressions;
using Application.Contracts.Caching;
using Infrastructure.Modules.Users.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Base;

/// <summary>
///     Base for write-side repositories: invalidate read cache after mutations.
///     Call <see cref="InvalidateUserLookupAsync" /> (or <see cref="RemoveCacheKeysAsync" />) after <c>SaveChanges</c>
///     when the aggregate identity (id/email/username) may already be cached on the read side.
///     New entities: invalidating by id/email/username is safe (removes nothing if keys were never set) and keeps behavior
///     consistent if list or negative caches are added later.
/// </summary>
public abstract class WriteRepositoryBase : RepositoryCacheBase
{
    protected WriteRepositoryBase(ICacheService cache) : base(cache)
    {
    }

    protected Task InvalidateUserLookupAsync(Guid? id, string? email, string? userName,
        CancellationToken cancellationToken)
    {
        List<string> keys = new(3);
        if (id is not null)
            keys.Add(UserReadCacheKeys.ById(id.Value));
        if (!string.IsNullOrEmpty(email))
            keys.Add(UserReadCacheKeys.ByEmail(email));
        if (!string.IsNullOrEmpty(userName))
            keys.Add(UserReadCacheKeys.ByUserName(userName));
        return RemoveCacheKeysAsync(keys, cancellationToken);
    }

    /// <summary>Reusable tracked lookup for command handlers that mutate entities.</summary>
    protected static Task<TEntity?> GetTrackedForUpdateAsync<TEntity>(
        DbSet<TEntity> set,
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        return set.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <summary>Reusable write-side add helper to keep concrete repositories concise.</summary>
    protected static void AddEntity<TEntity>(DbSet<TEntity> set, TEntity entity) where TEntity : class
    {
        set.Add(entity);
    }
}