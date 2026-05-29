using Application.Contracts.Billing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Modules.Billing;

public sealed class TenantRateLimitOptions
{
    public const string SectionName = "TenantRateLimit";

    public int AnonymousRequestsPerMinute { get; set; } = 60;

    public int DefaultRequestsPerMinute { get; set; } = 120;

    public int PlatformAdminRequestsPerMinute { get; set; } = 2_000;
}

public sealed class TenantRateLimitProvider : ITenantRateLimitProvider
{
    private readonly IEntitlementService _entitlements;
    private readonly IMemoryCache _cache;
    private readonly TenantRateLimitOptions _options;

    public TenantRateLimitProvider(
        IEntitlementService entitlements,
        IMemoryCache cache,
        IOptions<TenantRateLimitOptions> options)
    {
        _entitlements = entitlements;
        _cache = cache;
        _options = options.Value;
    }

    public int GetRequestsPerMinute(Guid? tenantId)
    {
        if (tenantId is null || tenantId == Guid.Empty)
            return _options.AnonymousRequestsPerMinute;

        string cacheKey = $"tenant-rate:{tenantId:N}";
        if (_cache.TryGetValue(cacheKey, out int cached))
            return cached;

        int resolved = ResolveRequestsPerMinuteAsync(tenantId.Value).GetAwaiter().GetResult();
        _cache.Set(cacheKey, resolved, TimeSpan.FromMinutes(5));
        return resolved;
    }

    private async Task<int> ResolveRequestsPerMinuteAsync(Guid tenantId)
    {
        PlanLimits? limits = await _entitlements.GetLimitsAsync(tenantId, CancellationToken.None);
        if (limits is not null && limits.ApiRequestsPerMinute > 0)
            return limits.ApiRequestsPerMinute;

        return _options.DefaultRequestsPerMinute;
    }
}
