namespace Application.Contracts.Messaging;

/// <summary>Publish messages to a broker (RabbitMQ exchange, Kafka topic, etc.).</summary>
public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default)
        where TMessage : class;
}

public sealed record MessagePublishContext(string TopicOrExchange, string? RoutingKey = null, string? MessageType = null);

/// <summary>Optional: subscribe to a queue/topic (Kafka consumer, Rabbit consumer).</summary>
public interface IMessageConsumer
{
    Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionName,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
