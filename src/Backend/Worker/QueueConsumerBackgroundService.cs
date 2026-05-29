using Application.Contracts.Messaging;

namespace Worker;

/// <summary>
///     Base worker for queue-driven consumers (RabbitMQ, Kafka, etc.).
///     Keep business logic in <see cref="HandleAsync" />, wire broker-specific adapter in Infrastructure.
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

    /// <summary>
    ///     Override to opt in to the broker's dead-letter routing. Default <c>null</c> preserves the
    ///     historical requeue-on-failure behaviour for sample / non-critical queues.
    /// </summary>
    protected virtual SubscriptionOptions? SubscriptionOptions => null;

    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _subscription = await _consumer.SubscribeAsync<TMessage>(
            SubscriptionName,
            HandleAsync,
            SubscriptionOptions,
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