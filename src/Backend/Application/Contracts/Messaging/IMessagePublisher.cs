namespace Application.Contracts.Messaging;

/// <summary>Publish messages to a broker (RabbitMQ exchange, Kafka topic, etc.).</summary>
public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Publishes an already-serialised payload (used by the outbox worker, which doesn't have
    /// the original message CLR type at hand). Implementations must publish <paramref name="payload"/>
    /// verbatim, applying only routing/headers from <paramref name="context"/>.
    /// </summary>
    Task PublishRawAsync(ReadOnlyMemory<byte> payload, MessagePublishContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-publish metadata the implementation forwards to the broker. The <paramref name="CorrelationId"/>
/// flows through AMQP's standard <c>correlation_id</c> property so consumers can stitch logs
/// across the API → outbox → worker boundary without re-parsing the payload.
/// </summary>
public sealed record MessagePublishContext(
    string TopicOrExchange,
    string? RoutingKey = null,
    string? MessageType = null,
    string? CorrelationId = null);

/// <summary>Optional: subscribe to a queue/topic (Kafka consumer, Rabbit consumer).</summary>
public interface IMessageConsumer
{
    Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionName,
        Func<TMessage, CancellationToken, Task> handler,
        SubscriptionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}

/// <summary>
/// Per-subscription tunables that broker-specific consumers honour. The default <c>null</c>
/// preserves the historical behaviour (requeue-on-failure, no DLQ) so existing call sites don't
/// change behaviour. Critical queues (media pipeline, payment events) should opt in to the DLQ
/// flow to bound retry blast-radius.
/// </summary>
/// <param name="EnableDeadLetterQueue">
/// When true, the broker is configured to route handler failures to a dead-letter queue (DLQ)
/// instead of re-delivering to the same consumer. Concrete RabbitMQ behaviour: the queue is
/// declared with <c>x-dead-letter-exchange</c> set; on handler exception the consumer issues
/// nack-without-requeue, and the broker routes the message to the configured DLQ for human
/// inspection. Retry semantics move to the application layer (e.g. media pipeline stale-recovery).
/// </param>
public sealed record SubscriptionOptions(bool EnableDeadLetterQueue = false);
