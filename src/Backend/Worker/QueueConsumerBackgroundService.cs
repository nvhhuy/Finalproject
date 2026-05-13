using Application.Contracts.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Worker;

/// <summary>
/// Base worker for queue-driven consumers (RabbitMQ, Kafka, etc.).
/// Keep business logic in <see cref="HandleAsync"/>, wire broker-specific adapter in Infrastructure.
/// </summary>
public abstract class QueueConsumerBackgroundService<TMessage> : BackgroundService where TMessage : class
{
    private readonly IMessageConsumer _consumer;
    private readonly ILogger _logger;
    private IDisposable? _subscription;

    protected QueueConsumerBackgroundService(IMessageConsumer consumer, ILogger logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected abstract string SubscriptionName { get; }

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscription = await _consumer.SubscribeAsync<TMessage>(
            SubscriptionName,
            HandleAsync,
            stoppingToken);

        _logger.LogInformation(
            "Queue consumer worker started for subscription '{Subscription}' ({MessageType}).",
            SubscriptionName,
            typeof(TMessage).Name);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
