using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Worker;
using Worker.Media;

namespace Worker.Tests.Media;

/// <summary>
/// Unit tests for <see cref="MediaStorageCleanupJob"/>. Mocks <c>IObjectStorage</c> to inject
/// a deterministic key listing and asserts the job deletes the right set of keys.
/// </summary>
public sealed class MediaStorageCleanupJobTests
{
    [Fact]
    public async Task ExecuteAsync_deletes_orphan_keys_whose_asset_id_has_no_db_row()
    {
        Guid ownerId = Guid.NewGuid();
        Guid liveAsset = Guid.NewGuid();
        Guid orphanAsset = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        var listing = new[]
        {
            new ObjectListing(KeyFor(ownerId, liveAsset, "original.jpg"), 1024, now.AddDays(-2)),
            new ObjectListing(KeyFor(ownerId, liveAsset, "thumb.jpg"), 256, now.AddDays(-2)),
            new ObjectListing(KeyFor(ownerId, orphanAsset, "original.mp4"), 2048, now.AddDays(-2)),
            new ObjectListing(KeyFor(ownerId, orphanAsset, "poster.jpg"), 512, now.AddDays(-2)),
        };

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.ListKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsync(listing));
        storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.ExistsAsync(liveAsset, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.ExistsAsync(orphanAsset, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        IServiceProvider sp = BuildScope(storage.Object, repo.Object, now);

        var job = new MediaStorageCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions
            {
                EnableMediaStorageCleanup = true,
                MediaStorageOrphanMinAgeHours = 1,
                MediaStorageCleanupBatchSize = 100,
            }),
            NullLogger<MediaStorageCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        // Only the orphan asset's keys should be deleted; live-asset keys must be untouched.
        storage.Verify(s => s.DeleteAsync(KeyFor(ownerId, orphanAsset, "original.mp4"), It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.DeleteAsync(KeyFor(ownerId, orphanAsset, "poster.jpg"), It.IsAny<CancellationToken>()), Times.Once);
        // Matcher must be at top-level — embedding It.IsAny inside the KeyFor helper eagerly
        // builds a literal string ("M.IsAny..."), which never matches the real keys.
        string liveAssetSegment = liveAsset.ToString("N");
        storage.Verify(s => s.DeleteAsync(It.Is<string>(k => k.Contains(liveAssetSegment)), It.IsAny<CancellationToken>()), Times.Never);

        // Existence check should be cached — both keys for the same asset must collapse to one DB call.
        repo.Verify(r => r.ExistsAsync(liveAsset, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.ExistsAsync(orphanAsset, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_skips_keys_younger_than_min_age_to_avoid_racing_uploads()
    {
        Guid ownerId = Guid.NewGuid();
        Guid orphanAsset = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        // Object is "freshly" uploaded — younger than the 24h minimum age. Likely an in-flight
        // CompleteMediaUpload that just hasn't landed yet; we must not delete it.
        var listing = new[]
        {
            new ObjectListing(KeyFor(ownerId, orphanAsset, "original.jpg"), 1024, now.AddMinutes(-5)),
        };

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.ListKeysAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsync(listing));

        var repo = new Mock<IMediaAssetRepository>();
        // We don't even expect ExistsAsync — the age guard short-circuits before the DB hit.

        IServiceProvider sp = BuildScope(storage.Object, repo.Object, now);

        var job = new MediaStorageCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions
            {
                EnableMediaStorageCleanup = true,
                MediaStorageOrphanMinAgeHours = 24,
            }),
            NullLogger<MediaStorageCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None);

        storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_is_noop_when_disabled()
    {
        var storage = new Mock<IObjectStorage>(MockBehavior.Strict); // any call would throw

        IServiceProvider sp = BuildScope(storage.Object, Mock.Of<IMediaAssetRepository>(), DateTime.UtcNow);

        var job = new MediaStorageCleanupJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new WorkerSchedulingOptions { EnableMediaStorageCleanup = false }),
            NullLogger<MediaStorageCleanupJob>.Instance);

        await job.ExecuteAsync(CancellationToken.None); // must not touch storage at all
    }

    private static string KeyFor(Guid owner, Guid asset, string filename) =>
        $"media/{owner:N}/{asset:N}/{filename}";

    private static IServiceProvider BuildScope(IObjectStorage storage, IMediaAssetRepository repo, DateTime now)
    {
        var services = new ServiceCollection();
        services.AddSingleton(storage);
        services.AddSingleton(repo);
        services.AddSingleton<IDateTimeProvider>(new FixedClock(now));
        return services.BuildServiceProvider();
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> source)
    {
        foreach (T item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        private readonly DateTime _now;
        public FixedClock(DateTime now) { _now = now; }
        public DateTime Now => _now;
    }
}
