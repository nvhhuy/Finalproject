using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.HealthChecks;

/// <summary>Verifies the distributed cache (Redis when configured, otherwise in-memory) responds to a round-trip.</summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private const string ProbeKey = "_healthcheck_:redis";
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.SetStringAsync(
                ProbeKey,
                DateTimeOffset.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);
            var value = await _cache.GetStringAsync(ProbeKey, cancellationToken);
            return value is null
                ? HealthCheckResult.Degraded("Cache write succeeded but read returned null.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cache probe failed.", ex);
        }
    }
}