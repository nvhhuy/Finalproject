namespace Application.Contracts.Caching;

/// <summary>Per-call cache control for read repositories and queries.</summary>
public readonly record struct CacheOptions(bool Enabled = true, TimeSpan? TimeToLive = null)
{
    public static CacheOptions Disabled => new(false);

    public static CacheOptions DefaultTtl(TimeSpan ttl)
    {
        return new CacheOptions(true, ttl);
    }
}