using System.Text.RegularExpressions;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Scheduling;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Media;

/// <summary>
/// Sweeps the media bucket for orphan objects — files that have outlived their <c>MediaAsset</c>
/// row in the database. Two classes of orphans accumulate over time:
/// <list type="number">
///   <item><description><b>Abandoned uploads</b>: client requested a presigned URL via <c>InitiateMediaUpload</c>, PUT the bytes, then never called <c>CompleteMediaUpload</c>. The row stays <c>Pending</c> in the DB, but it could also disappear if the cleanup of pending uploads runs first. Either way, the bytes are useless.</description></item>
///   <item><description><b>Deleted assets that lost a race</b>: <c>DeleteMediaAssetCommand</c> issues storage <c>DELETE</c>s and removes the DB row in the same transaction. If a storage <c>DELETE</c> failed mid-loop (network blip), some keys are left behind. The DB row is gone.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>Algorithm:</b> walks every key under the configured <c>media/</c> prefix; for each
/// candidate, extracts the asset-id segment and asks the repository whether a row exists. If
/// not — and the object is older than <c>MediaStorageOrphanMinAgeHours</c> — issues a DELETE.
/// The age guard prevents the race where a freshly-PUT object hasn't yet had its
/// <c>CompleteMediaUpload</c> call land.</para>
///
/// <para><b>Why opt-in:</b> the bucket scan is O(total objects), not O(orphans). For very large
/// deployments operators may prefer to invoke this manually or run it on a separate maintenance
/// host. Default is disabled; flip <c>WorkerSchedulingOptions.EnableMediaStorageCleanup</c> to
/// turn it on.</para>
/// </remarks>
public sealed class MediaStorageCleanupJob : ICronJob
{
    private const string MediaPrefix = "media/";

    /// <summary>
    /// Extracts the asset-id from a key shaped like <c>media/{owner}/{asset}/...</c>. The two
    /// guid segments are 32 hex chars each ({N} format); we match them as such instead of as
    /// generic path segments to skip keys with unexpected layouts (which we leave alone).
    /// </summary>
    private static readonly Regex AssetIdPattern = new(
        @"^media/[0-9a-f]{32}/(?<assetId>[0-9a-f]{32})/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<MediaStorageCleanupJob> _logger;

    public MediaStorageCleanupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<MediaStorageCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(MediaStorageCleanupJob);
    public string CronExpression => _options.MediaStorageCleanupCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableMediaStorageCleanup)
        {
            _logger.LogDebug("MediaStorageCleanupJob disabled by configuration; skipping tick.");
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var repo = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        DateTime cutoff = clock.Now.AddHours(-_options.MediaStorageOrphanMinAgeHours);
        int scanned = 0, deleted = 0;
        // Cache the asset-id → exists lookups so a directory with 20 variants for the same asset
        // only hits the DB once. Most media assets emit 8-12+ variants so this matters.
        var existsCache = new Dictionary<Guid, bool>();

        try
        {
            await foreach (ObjectListing entry in storage.ListKeysAsync(MediaPrefix, stoppingToken))
            {
                scanned++;
                if (entry.LastModifiedUtc > cutoff)
                    continue; // Too young — skip to avoid racing with in-flight uploads

                Match match = AssetIdPattern.Match(entry.Key);
                if (!match.Success)
                    continue; // Unexpected layout — leave alone

                if (!Guid.TryParseExact(match.Groups["assetId"].Value, "N", out Guid assetId))
                    continue;

                if (!existsCache.TryGetValue(assetId, out bool exists))
                {
                    exists = await repo.ExistsAsync(assetId, stoppingToken);
                    existsCache[assetId] = exists;
                }
                if (exists)
                    continue;

                await storage.DeleteAsync(entry.Key, stoppingToken);
                deleted++;
                _logger.LogInformation(
                    "Cleanup deleted orphan {Key} ({Bytes:N0} B, last modified {LastModified:o})",
                    entry.Key, entry.ByteSize, entry.LastModifiedUtc);

                if (deleted >= _options.MediaStorageCleanupBatchSize)
                {
                    _logger.LogWarning(
                        "Cleanup batch size {Batch} hit — deferring remaining orphans to next tick.",
                        _options.MediaStorageCleanupBatchSize);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't let a single bad listing kill the whole job — log and bail. Next tick retries.
            _logger.LogError(ex, "MediaStorageCleanupJob aborted mid-scan");
        }

        _logger.LogInformation(
            "MediaStorageCleanupJob tick complete: scanned={Scanned}, deleted={Deleted}, assets-checked={Cached}",
            scanned, deleted, existsCache.Count);
    }
}
