namespace Application.Contracts.Caching;

public static class CacheServiceExtensions
{
    public static async Task<T?> GetOrSetAsync<T>(
        this ICacheService cache,
        string key,
        Func<Task<T?>> factory,
        TimeSpan ttl,
        bool useCache,
        CancellationToken cancellationToken = default)
    {
        if (useCache)
        {
            T? existing = await cache.GetAsync<T>(key, cancellationToken);
            if (existing is not null)
                return existing;
        }

        T? created = await factory();
        if (useCache && created is not null)
            await cache.SetAsync(key, created, ttl, cancellationToken);
        return created;
    }
}
