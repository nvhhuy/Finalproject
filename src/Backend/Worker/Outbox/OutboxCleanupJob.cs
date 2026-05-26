using Application.Contracts.Scheduling;
using Application.Contracts.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Worker.Outbox;

/// <summary>
/// Periodic cleanup of fully-processed outbox rows. Without this the <c>OutboxMessages</c> table
/// grows monotonically — even at modest traffic (10 events/sec) that's ~1 M rows/day, which
/// turns the SKIP-LOCKED dispatch query into a sequential scan after a few months.
/// </summary>
/// <remarks>
/// <para><b>Safety:</b> only deletes rows that have <em>completed their lifecycle</em>:
/// either <see cref="Infrastructure.Messaging.Outbox.OutboxMessage.ProcessedOnUtc"/> (successful
/// publish) OR <see cref="Infrastructure.Messaging.Outbox.OutboxMessage.AbandonedAtUtc"/> (gave
/// up after max attempts). Abandoned rows get a configurably-longer retention window
/// (<see cref="WorkerSchedulingOptions.OutboxAbandonedRetentionDays"/>) so an on-call engineer
/// has enough time to replay them out of the failure log before they're gone.</para>
///
/// <para><b>Batching:</b> deletes in chunks of <see cref="WorkerSchedulingOptions.OutboxCleanupBatchSize"/>
/// per query so a long-overdue cleanup doesn't take a multi-minute lock on the table. Continues
/// until either a partial batch comes back (nothing left to delete) or the stoppingToken trips.</para>
///
/// <para><b>Why not just <c>TRUNCATE</c>:</b> we never want to delete pending rows. EF Core's
/// <c>ExecuteDeleteAsync</c> with a <c>LIMIT</c>-friendly query gives us safe, bounded deletes
/// without raw SQL.</para>
/// </remarks>
public sealed class OutboxCleanupJob : ICronJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerSchedulingOptions _options;
    private readonly ILogger<OutboxCleanupJob> _logger;

    public OutboxCleanupJob(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerSchedulingOptions> options,
        ILogger<OutboxCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => nameof(OutboxCleanupJob);
    public string CronExpression => _options.OutboxCleanupCronExpression;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        DateTime processedCutoff = clock.Now.AddDays(-Math.Max(1, _options.OutboxRetentionDays));
        DateTime abandonedCutoff = clock.Now.AddDays(-Math.Max(1, _options.OutboxAbandonedRetentionDays));
        int batchSize = Math.Max(100, _options.OutboxCleanupBatchSize);
        int totalDeleted = 0;

        try
        {
            // Clean processed rows first (much larger volume in steady state), then abandoned
            // rows with their longer retention. Two passes keep the SQL simple and let an
            // operator change either retention independently without re-running both.
            totalDeleted += await CleanAsync(
                db,
                m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < processedCutoff,
                batchSize,
                "processed",
                stoppingToken);

            totalDeleted += await CleanAsync(
                db,
                m => m.AbandonedAtUtc != null && m.AbandonedAtUtc < abandonedCutoff,
                batchSize,
                "abandoned",
                stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't bring down the host on a cleanup failure — log and let the next tick retry.
            _logger.LogError(ex, "OutboxCleanupJob aborted after deleting {Total} rows", totalDeleted);
            return;
        }

        _logger.LogInformation(
            "OutboxCleanupJob tick complete: deleted {Total} rows (processed cutoff {ProcessedCutoff:o}, abandoned cutoff {AbandonedCutoff:o})",
            totalDeleted, processedCutoff, abandonedCutoff);
    }

    private async Task<int> CleanAsync(
        ApplicationDbContext db,
        System.Linq.Expressions.Expression<Func<Infrastructure.Messaging.Outbox.OutboxMessage, bool>> predicate,
        int batchSize,
        string bucket,
        CancellationToken stoppingToken)
    {
        int total = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            // EF Core 8's ExecuteDeleteAsync compiles to a single DELETE statement (no
            // round-trip-per-row), which is essential for batches of thousands. We bound
            // the batch via Where + Take + ExecuteDeleteAsync — Npgsql translates to
            // `DELETE … WHERE Id IN (SELECT Id FROM … LIMIT N)`.
            int deleted = await db.OutboxMessages
                .Where(predicate)
                .Take(batchSize)
                .ExecuteDeleteAsync(stoppingToken);

            if (deleted == 0) break;
            total += deleted;

            _logger.LogDebug("OutboxCleanupJob deleted {Batch} {Bucket} rows (running total {Total})",
                deleted, bucket, total);

            // Stop after a single partial batch — the next scheduled run picks up the rest.
            if (deleted < batchSize) break;
        }
        return total;
    }
}
