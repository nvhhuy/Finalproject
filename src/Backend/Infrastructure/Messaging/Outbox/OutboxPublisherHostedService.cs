using System.Text;
using Application.Contracts.Messaging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Outbox;

/// <summary>
///     Drains <see cref="OutboxMessage" /> rows produced by the transactional outbox interceptor and
///     publishes their payloads to RabbitMQ via <see cref="IMessagePublisher" />. Failed publishes are
///     retried with exponential backoff up to <see cref="OutboxOptions.MaxAttempts" />.
/// </summary>
public sealed class OutboxPublisherHostedService : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);
    private readonly ILogger<OutboxPublisherHostedService> _logger;
    private readonly IOptionsMonitor<OutboxOptions> _options;

    private readonly IServiceProvider _services;

    public OutboxPublisherHostedService(
        IServiceProvider services,
        IOptionsMonitor<OutboxOptions> options,
        ILogger<OutboxPublisherHostedService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var snapshot = _options.CurrentValue;
        if (!snapshot.Enabled)
        {
            _logger.LogInformation("Outbox publisher disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "Outbox publisher started. PollInterval={Interval}s BatchSize={BatchSize} MaxAttempts={MaxAttempts}",
            snapshot.PollIntervalSeconds,
            snapshot.BatchSize,
            snapshot.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                    await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox cycle failed; backing off for one poll interval.");
                await Task.Delay(TimeSpan.FromSeconds(_options.CurrentValue.PollIntervalSeconds), stoppingToken);
            }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        return await ProcessBatchAsync(db, publisher, _options.CurrentValue, _logger, cancellationToken);
    }

    /// <summary>
    ///     Pure pipeline used by the hosted service; exposed for unit tests so callers can pass in
    ///     an in-memory <see cref="ApplicationDbContext" /> and a stub publisher without spinning up DI.
    /// </summary>
    /// <remarks>
    ///     On PostgreSQL we open an explicit transaction and load the batch with
    ///     <c>SELECT ... FOR UPDATE SKIP LOCKED</c> so that running multiple worker instances does
    ///     not cause double-publishing. On EF Core InMemory (used by tests) we fall back to a plain
    ///     query because the provider does not support row-level locking.
    /// </remarks>
    internal static async Task<int> ProcessBatchAsync(
        ApplicationDbContext db,
        IMessagePublisher publisher,
        OutboxOptions opts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return await ProcessBatchCoreAsync(
                db, publisher, opts, logger, false, cancellationToken);

        // NpgsqlRetryingExecutionStrategy requires the explicit transaction to live inside
        // CreateExecutionStrategy().ExecuteAsync — otherwise FOR UPDATE SKIP LOCKED fails.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(ct);

            var count = await ProcessBatchCoreAsync(
                db, publisher, opts, logger, true, ct);

            await transaction.CommitAsync(ct);
            return count;
        }, cancellationToken);
    }

    private static async Task<int> ProcessBatchCoreAsync(
        ApplicationDbContext db,
        IMessagePublisher publisher,
        OutboxOptions opts,
        ILogger logger,
        bool useSkipLocked,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var batch = useSkipLocked
            ? await LoadBatchWithSkipLockedAsync(db, opts.BatchSize, now, cancellationToken)
            : await db.OutboxMessages
                // Pending = not published AND not abandoned AND eligible for next attempt. The
                // abandoned filter prevents the publisher from re-trying poison messages on every
                // tick after MarkAbandoned has been recorded.
                .Where(m => m.ProcessedOnUtc == null
                            && m.AbandonedAtUtc == null
                            && (m.NextAttemptUtc == null || m.NextAttemptUtc <= now))
                .OrderBy(m => m.OccurredOnUtc)
                .Take(opts.BatchSize)
                .ToListAsync(cancellationToken);

        if (batch.Count == 0)
            return 0;

        foreach (var row in batch)
            try
            {
                var context = new MessagePublishContext(
                    row.Exchange ?? string.Empty,
                    row.RoutingKey,
                    row.Type,
                    row.CorrelationId);
                ReadOnlyMemory<byte> payload = Encoding.UTF8.GetBytes(row.PayloadJson);
                await publisher.PublishRawAsync(payload, context, cancellationToken);
                row.MarkProcessed(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                if (row.Attempts + 1 >= opts.MaxAttempts)
                {
                    logger.LogError(ex,
                        "Outbox message {EventId} abandoned after {Attempts} attempts.",
                        row.EventId,
                        row.Attempts + 1);
                    row.MarkAbandoned(DateTime.UtcNow, ex.Message);
                }
                else
                {
                    var delay = ComputeBackoff(opts.InitialBackoffSeconds, row.Attempts);
                    logger.LogWarning(ex,
                        "Outbox publish failed for {EventId} (attempt {Attempts}); retrying in {Delay}s.",
                        row.EventId,
                        row.Attempts + 1,
                        delay.TotalSeconds);
                    row.RecordFailure(DateTime.UtcNow, ex.Message, delay);
                }
            }

        await db.SaveChangesAsync(cancellationToken);
        return batch.Count;
    }

    /// <summary>
    ///     PostgreSQL-only pessimistic claim. <c>FOR UPDATE SKIP LOCKED</c> ensures concurrent
    ///     outbox workers will silently skip rows already held by a peer's transaction instead of
    ///     blocking on them, so the only contention overhead is the per-row lock acquisition.
    /// </summary>
    private static Task<List<OutboxMessage>> LoadBatchWithSkipLockedAsync(
        ApplicationDbContext db,
        int batchSize,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return db.OutboxMessages
            .FromSqlInterpolated(
                $@"SELECT * FROM ""OutboxMessages""
                   WHERE ""ProcessedOnUtc"" IS NULL
                     AND ""AbandonedAtUtc"" IS NULL
                     AND (""NextAttemptUtc"" IS NULL OR ""NextAttemptUtc"" <= {now})
                   ORDER BY ""OccurredOnUtc""
                   LIMIT {batchSize}
                   FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);
    }

    private static TimeSpan ComputeBackoff(int initialSeconds, int previousAttempts)
    {
        var seconds = initialSeconds * Math.Pow(2, previousAttempts);
        if (seconds > MaxBackoff.TotalSeconds || double.IsInfinity(seconds))
            return MaxBackoff;
        return TimeSpan.FromSeconds(seconds);
    }
}