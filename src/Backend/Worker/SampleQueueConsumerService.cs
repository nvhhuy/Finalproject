using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace Worker;

/// <summary>Example queue consumer worker. Replace <see cref="SampleUserEvent"/> with real integration events.</summary>
public sealed class SampleQueueConsumerService : QueueConsumerBackgroundService<SampleUserEvent>
{
    private readonly ILogger<SampleQueueConsumerService> _logger;

    public SampleQueueConsumerService(
        IMessageConsumer consumer,
        ILogger<SampleQueueConsumerService> logger) : base(consumer, logger)
    {
        _logger = logger;
    }

    protected override string SubscriptionName => "users.events.sample";

    protected override Task HandleAsync(SampleUserEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sample queue message consumed. UserId={UserId}, EventType={EventType}, OccurredAt={OccurredAtUtc}",
            message.UserId,
            message.EventType,
            message.OccurredAtUtc);
        return Task.CompletedTask;
    }
}

/// <summary>Minimal sample integration event shape for queue-worker scaffolding.</summary>
public sealed record SampleUserEvent(Guid UserId, string EventType, DateTimeOffset OccurredAtUtc);
