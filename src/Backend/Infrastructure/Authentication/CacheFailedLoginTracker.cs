using Application.Contracts.Authentication;
using Application.Contracts.Caching;
using Microsoft.Extensions.Options;

namespace Infrastructure.Authentication;

/// <summary>
///     Cache-backed implementation of <see cref="IFailedLoginTracker" />. Two keys per identity:
///     a counter with a rolling TTL and an explicit lockout marker carrying the lockout expiry.
/// </summary>
internal sealed class CacheFailedLoginTracker : IFailedLoginTracker
{
    private const string CounterPrefix = "auth:loginfail:count:";
    private const string LockPrefix = "auth:loginfail:lock:";

    private readonly ICacheService _cache;
    private readonly IOptionsMonitor<AccountLockoutOptions> _options;

    public CacheFailedLoginTracker(ICacheService cache, IOptionsMonitor<AccountLockoutOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public async Task<TimeSpan?> GetLockoutRemainingAsync(string identity,
        CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(identity);
        if (normalised.Length == 0)
            return null;

        var lockUntil = await _cache.GetAsync<DateTimeOffset?>(LockKey(normalised), cancellationToken);
        if (lockUntil is null)
            return null;

        var remaining = lockUntil.Value - DateTimeOffset.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public async Task<bool> RegisterFailureAsync(string identity, CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(identity);
        if (normalised.Length == 0)
            return false;

        var opts = _options.CurrentValue;
        if (opts.MaxFailures <= 0)
            return false;

        var current = await _cache.GetAsync<int?>(CounterKey(normalised), cancellationToken) ?? 0;
        current += 1;
        await _cache.SetAsync(CounterKey(normalised), current, TimeSpan.FromSeconds(opts.WindowSeconds),
            cancellationToken);

        if (current < opts.MaxFailures)
            return false;

        var lockUntil = DateTimeOffset.UtcNow.AddSeconds(opts.LockoutSeconds);
        await _cache.SetAsync(LockKey(normalised), lockUntil, TimeSpan.FromSeconds(opts.LockoutSeconds),
            cancellationToken);
        return true;
    }

    public Task ResetAsync(string identity, CancellationToken cancellationToken = default)
    {
        var normalised = Normalise(identity);
        if (normalised.Length == 0)
            return Task.CompletedTask;

        return _cache.RemoveManyAsync(new[] { CounterKey(normalised), LockKey(normalised) }, cancellationToken);
    }

    private static string Normalise(string identity)
    {
        return string.IsNullOrWhiteSpace(identity) ? string.Empty : identity.Trim().ToLowerInvariant();
    }

    private static string CounterKey(string identity)
    {
        return CounterPrefix + identity;
    }

    private static string LockKey(string identity)
    {
        return LockPrefix + identity;
    }
}