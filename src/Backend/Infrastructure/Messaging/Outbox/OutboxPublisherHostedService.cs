using Application.Contracts.Messaging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Infrastructure.Messaging.Outbox;

/// <summary>
/// Drains <see cref="OutboxMessage"/> rows produced by the transactional outbox interceptor and
/// publishes their payloads to RabbitMQ via <see cref="IMessagePublisher"/>. Failed publishes are
/// retried with exponential backoff up to <see cref="OutboxOptions.MaxAttempts"/>.
/// </summary>
public sealed class OutboxPublisherHostedService : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<OutboxOptions> _options;
    private readonly ILogger<OutboxPublisherHostedService> _logger;

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
        OutboxOptions snapshot = _options.CurrentValue;
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
        {
            try
            {
                int processed = await ProcessBatchAsync(stoppingToken);
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
    }

    private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IMessagePublisher publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        return await ProcessBatchAsync(db, publisher, _options.CurrentValue, _logger, cancellationToken);
    }

    /// <summary>
    /// Pure pipeline used by the hosted service; exposed for unit tests so callers can pass in
    /// an in-memory <see cref="ApplicationDbContext"/> and a stub publisher without spinning up DI.
    /// </summary>
    internal static async Task<int> ProcessBatchAsync(
        ApplicationDbContext db,
        IMessagePublisher publisher,
        OutboxOptions opts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        List<OutboxMessage> batch = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && (m.NextAttemptUtc == null || m.NextAttemptUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(opts.BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
            return 0;

        foreach (OutboxMessage row in batch)
        {
            try
            {
                var context = new MessagePublishContext(
                    TopicOrExchange: row.Exchange ?? string.Empty,
                    RoutingKey: row.RoutingKey,
                    MessageType: row.Type);
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
                    TimeSpan delay = ComputeBackoff(opts.InitialBackoffSeconds, row.Attempts);
                    logger.LogWarning(ex,
                        "Outbox publish failed for {EventId} (attempt {Attempts}); retrying in {Delay}s.",
                        row.EventId,
                        row.Attempts + 1,
                        delay.TotalSeconds);
                    row.RecordFailure(DateTime.UtcNow, ex.Message, delay);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return batch.Count;
    }

    private static TimeSpan ComputeBackoff(int initialSeconds, int previousAttempts)
    {
        double seconds = initialSeconds * Math.Pow(2, previousAttempts);
        if (seconds > MaxBackoff.TotalSeconds || double.IsInfinity(seconds))
            return MaxBackoff;
        return TimeSpan.FromSeconds(seconds);
    }
}
