using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Scheduling;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.MediaAggregate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Media;

/// <summary>
/// Detects and requeues media assets that have been stuck in <see cref="MediaAssetStatus.Processing"/>
/// longer than the configured threshold. Without this, a worker that crashes mid-pipeline (OOM,
/// container kill, ffmpeg hang) would leave the asset permanently unprocessable — the API would
/// report <c>Processing</c> forever and the user would never see a Ready variant.
/// </summary>
/// <remarks>
/// <para><b>Design:</b> the job acts as a watchdog only — it doesn't run encoding itself. It finds
/// stale assets, calls <see cref="MediaAsset.RequeueAfterStall"/> which raises a fresh upload
/// event, commits, and the outbox publisher pushes that event back onto the worker queue. The
/// next available worker picks it up and the regular pipeline runs.</para>
///
/// <para><b>Idempotency:</b> if two recovery instances ran concurrently (e.g. two worker pods)
/// and both grabbed the same stalled asset, the second one's <c>RequeueAfterStall</c> would throw
/// because the first one already transitioned the status to <c>Uploaded</c>. The throw is caught
/// per-asset so one collision doesn't sink the whole batch.</para>
///
/// <para><b>Concurrency with live worker:</b> if the original worker is still alive but very
/// slow (not crashed), it'll eventually call <c>MarkReady</c> or <c>MarkFailed</c>. Because
/// recovery resets the asset to <c>Uploaded</c>, the slow worker's <c>MarkReady</c> will throw
/// (status guard mismatch) and the pipeline's catch block will <c>MarkFailed</c>. That's a benign
/// outcome — the second worker (started by recovery) takes over.</para>
/// </remarks>
public sealed class StaleMediaRecoveryJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<StaleMediaRecoveryJob> _logger;

    public StaleMediaRecoveryJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<StaleMediaRecoveryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(StaleMediaRecoveryJob);

    public string CronExpression => _options.StaleMediaRecoveryCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Per-tick scope keeps DbContext lifetime bounded and prevents change-tracking leaks
        // between ticks (the job lives forever as a singleton, the scope is per-execution).
        using IServiceScope scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        DateTime cutoff = clock.Now.AddMinutes(-_options.StaleMediaProcessingThresholdMinutes);
        IReadOnlyList<MediaAsset> stalled = await repo.GetStalledProcessingAsync(
            cutoff,
            _options.StaleMediaRecoveryBatchSize,
            stoppingToken);

        if (stalled.Count == 0)
            return;

        int requeued = 0;
        int quarantined = 0;
        foreach (MediaAsset asset in stalled)
        {
            try
            {
                int attemptsBefore = asset.ProcessingAttemptCount;
                asset.RequeueAfterStall(clock.Now, _options.MaxMediaProcessingAttempts);

                if (asset.Status == Domains.MediaAggregate.MediaAssetStatus.Failed)
                {
                    quarantined++;
                    _logger.LogError(
                        "Quarantined poison media asset {AssetId} after {Attempts} attempts: {Error}",
                        asset.Id.Value,
                        attemptsBefore,
                        asset.ErrorMessage);
                }
                else
                {
                    requeued++;
                    _logger.LogWarning(
                        "Requeued stalled media asset {AssetId} (owner {OwnerId}, attempt {Attempt}/{Max}, started {Started:o}, age {AgeMin:N1} min)",
                        asset.Id.Value,
                        asset.OwnerUserId,
                        attemptsBefore,
                        _options.MaxMediaProcessingAttempts,
                        asset.ProcessingStartedAtUtc,
                        (clock.Now - (asset.ProcessingStartedAtUtc ?? clock.Now)).TotalMinutes);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Another recovery instance beat us to this asset, or the live worker transitioned
                // it concurrently. Skip — the asset is no longer our problem this tick.
                _logger.LogInformation(ex, "Skipped media asset {AssetId} during stale recovery", asset.Id.Value);
            }
        }

        if (requeued + quarantined > 0)
            await uow.SaveChangesAsync(stoppingToken);

        _logger.LogInformation(
            "Stale media recovery tick: {Scanned} candidates, {Requeued} requeued, {Quarantined} quarantined",
            stalled.Count,
            requeued,
            quarantined);
    }
}
