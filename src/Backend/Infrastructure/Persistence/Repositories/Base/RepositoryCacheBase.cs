using Application.Contracts.Caching;

namespace Infrastructure.Persistence.Repositories.Base;

/// <summary>Shared cache access for read and write repository bases.</summary>
public abstract class RepositoryCacheBase
{
    protected readonly ICacheService Cache;

    protected RepositoryCacheBase(ICacheService cache) => Cache = cache;

    /// <summary>Removes a single cache entry (no-op if key is null/empty).</summary>
    protected Task RemoveCacheKeyAsync(string? key, CancellationToken cancellationToken = default) =>
        string.IsNullOrEmpty(key) ? Task.CompletedTask : Cache.RemoveAsync(key, cancellationToken);

    /// <summary>Removes many keys in one round-trip (empty input is ignored).</summary>
    protected Task RemoveCacheKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        string[] distinct = keys.Where(static k => !string.IsNullOrEmpty(k)).Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 0 ? Task.CompletedTask : Cache.RemoveManyAsync(distinct, cancellationToken);
    }
}
