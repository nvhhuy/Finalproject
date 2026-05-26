using Application.Contracts.Caching;
using Application.Contracts.Settings;
using Application.Contracts.Transactions;
using Domains.SystemSettings;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Settings;

/// <summary>
/// Cached read-through implementation of <see cref="IAppSettingsService"/>.
/// </summary>
/// <remarks>
/// App settings (<c>allow_public_register</c>, etc.) are read on nearly every request but change
/// very rarely, so we keep the latest value in <see cref="ICacheService"/> for
/// <see cref="CacheTtl"/>. <see cref="SetValueAsync(string, string, CancellationToken)"/> also
/// invalidates the entry so administrators see their change reflected immediately. On a
/// multi-node deployment the Redis backend keeps the invalidation broadcast-safe.
/// </remarks>
public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "appsettings:";

    private readonly ApplicationDbContext _write;
    private readonly ApplicationReadDbContext _read;
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;

    public AppSettingsService(
        ApplicationDbContext write,
        ApplicationReadDbContext read,
        IUnitOfWork uow,
        ICacheService cache)
    {
        _write = write;
        _read = read;
        _uow = uow;
        _cache = cache;
    }

    public async Task<bool> GetBooleanAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        string? value = await GetValueAsync(key, cancellationToken);
        return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        string cacheKey = CacheKeyPrefix + key;
        AppSettingCacheEntry? cached = await _cache.GetAsync<AppSettingCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached.Value;

        AppSetting? setting = await _read.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        await _cache.SetAsync(cacheKey, new AppSettingCacheEntry(setting?.Value), CacheTtl, cancellationToken);
        return setting?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        AppSetting? existing = await _write.AppSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            _write.AppSettings.Add(AppSetting.Create(key, value));
        }
        else
        {
            existing.SetValue(value);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        // Invalidate after commit so concurrent readers don't repopulate the cache with the old value.
        await _cache.RemoveAsync(CacheKeyPrefix + key, cancellationToken);
    }

    /// <summary>
    /// Wrapper so that "key exists but value is null" can be cached distinctly from a cache miss.
    /// </summary>
    public sealed record AppSettingCacheEntry(string? Value);
}
