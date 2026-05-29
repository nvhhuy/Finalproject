using System.Collections.Concurrent;
using Application.Contracts.Caching;
using Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Authentication;

public sealed class CacheFailedLoginTrackerTests
{
    [Fact]
    public async Task GetLockoutRemaining_returns_null_when_no_failures_recorded()
    {
        var tracker = NewTracker(out _);

        var remaining = await tracker.GetLockoutRemainingAsync("nobody@example.com");

        Assert.Null(remaining);
    }

    [Fact]
    public async Task RegisterFailure_below_threshold_does_not_lock()
    {
        var tracker = NewTracker(out _, opts => opts.MaxFailures = 3);

        var locked1 = await tracker.RegisterFailureAsync("user@example.com");
        var locked2 = await tracker.RegisterFailureAsync("user@example.com");

        Assert.False(locked1);
        Assert.False(locked2);
        Assert.Null(await tracker.GetLockoutRemainingAsync("user@example.com"));
    }

    [Fact]
    public async Task RegisterFailure_at_threshold_locks_identity()
    {
        var tracker = NewTracker(out _, opts =>
        {
            opts.MaxFailures = 2;
            opts.LockoutSeconds = 60;
        });

        await tracker.RegisterFailureAsync("user@example.com");
        var locked = await tracker.RegisterFailureAsync("user@example.com");

        Assert.True(locked);
        var remaining = await tracker.GetLockoutRemainingAsync("user@example.com");
        Assert.NotNull(remaining);
        Assert.InRange(remaining!.Value.TotalSeconds, 1, 60);
    }

    [Fact]
    public async Task Identity_comparison_is_case_and_whitespace_insensitive()
    {
        var tracker = NewTracker(out _, opts => opts.MaxFailures = 2);

        await tracker.RegisterFailureAsync(" User@Example.com ");
        var locked = await tracker.RegisterFailureAsync("user@example.com");

        Assert.True(locked);
    }

    [Fact]
    public async Task Reset_clears_both_counter_and_lock()
    {
        var tracker = NewTracker(out var cache, opts =>
        {
            opts.MaxFailures = 1;
            opts.LockoutSeconds = 60;
        });

        await tracker.RegisterFailureAsync("user@example.com");
        Assert.NotNull(await tracker.GetLockoutRemainingAsync("user@example.com"));

        await tracker.ResetAsync("user@example.com");

        Assert.Null(await tracker.GetLockoutRemainingAsync("user@example.com"));
        Assert.Empty(cache.Snapshot);
    }

    [Fact]
    public async Task Empty_identity_is_ignored()
    {
        var tracker = NewTracker(out var cache);

        var locked = await tracker.RegisterFailureAsync("   ");
        var remaining = await tracker.GetLockoutRemainingAsync("");

        Assert.False(locked);
        Assert.Null(remaining);
        Assert.Empty(cache.Snapshot);
    }

    private static CacheFailedLoginTracker NewTracker(out InMemoryCache cache,
        Action<AccountLockoutOptions>? configure = null)
    {
        var options = new AccountLockoutOptions();
        configure?.Invoke(options);
        var monitor = new TestOptionsMonitor<AccountLockoutOptions>(options);
        cache = new InMemoryCache();
        return new CacheFailedLoginTracker(cache, monitor);
    }
}

internal sealed class InMemoryCache : ICacheService
{
    private readonly ConcurrentDictionary<string, object?> _store = new();

    public IReadOnlyDictionary<string, object?> Snapshot => _store;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value))
            return Task.FromResult((T?)value);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
            _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public TestOptionsMonitor(T value)
    {
        CurrentValue = value;
    }

    public T CurrentValue { get; }

    public T Get(string? name)
    {
        return CurrentValue;
    }

    public IDisposable OnChange(Action<T, string?> listener)
    {
        return new EmptyDisposable();
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}