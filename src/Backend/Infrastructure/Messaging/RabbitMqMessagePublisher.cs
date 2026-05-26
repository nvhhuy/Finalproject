using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

/// <summary>
/// Publishes to RabbitMQ via a singleton connection. <see cref="IModel"/> (channel) is NOT thread-safe
/// in RabbitMQ.Client 6.x, so every <c>BasicPublish</c> goes through <see cref="_publishLock"/>. For
/// higher throughput this can be swapped to a channel pool or RabbitMQ.Client 7.x async API.
/// </summary>
public sealed class RabbitMqMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly object _publishLock = new();

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessagePublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        // DispatchConsumersAsync is unused for the publisher (no consumer on this connection)
        // but we keep the factory shape symmetric with RabbitMqMessageConsumer so an operator
        // looking at the two side-by-side doesn't think the difference is meaningful.
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true,
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.DefaultExchange, ExchangeType.Topic, durable: true);

        // confirm.select moves the channel into publisher-confirms mode. Every BasicPublish
        // gets an ack/nack from the broker; WaitForConfirmsOrDie below blocks until either
        // the ack arrives or the broker times out. Without this, a publish that the broker
        // silently drops (no matching queue, in-memory message lost during failover) returns
        // success to .NET and the outbox marks the row processed.
        _channel.ConfirmSelect();
    }

    public Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        string exchange = string.IsNullOrWhiteSpace(context.TopicOrExchange) ? _options.DefaultExchange : context.TopicOrExchange;
        string routingKey = context.RoutingKey ?? typeof(TMessage).Name;
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        PublishSynchronized(exchange, routingKey, body, context.CorrelationId);
        _logger.LogDebug("Published {Type} to exchange {Exchange} routing {RoutingKey}", typeof(TMessage).Name, exchange, routingKey);
        return Task.CompletedTask;
    }

    public Task PublishRawAsync(ReadOnlyMemory<byte> payload, MessagePublishContext context, CancellationToken cancellationToken = default)
    {
        string exchange = string.IsNullOrWhiteSpace(context.TopicOrExchange) ? _options.DefaultExchange : context.TopicOrExchange;
        string routingKey = context.RoutingKey ?? context.MessageType ?? "outbox";

        PublishSynchronized(exchange, routingKey, payload, context.CorrelationId);
        _logger.LogDebug("Published raw payload ({Bytes}b) to exchange {Exchange} routing {RoutingKey}", payload.Length, exchange, routingKey);
        return Task.CompletedTask;
    }

    private void PublishSynchronized(string exchange, string routingKey, ReadOnlyMemory<byte> body, string? correlationId)
    {
        lock (_publishLock)
        {
            // BasicProperties must be created from the channel (otherwise the client throws).
            // We populate `correlation_id` as a top-level AMQP property AND mirror it into the
            // headers table — top-level so tooling like the RabbitMQ management UI surfaces it,
            // headers so consumers that don't read properties still get the value.
            //
            // Persistence: every publish marks DeliveryMode=2 (persistent) so a broker restart
            // before the message reaches a durable queue doesn't lose it. The outbox table is
            // already durable on our side, so the cost is symmetric.
            IBasicProperties props = _channel.CreateBasicProperties();
            props.Persistent = true;
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                props.CorrelationId = correlationId;
                props.Headers = new Dictionary<string, object> { ["correlation-id"] = correlationId };
            }
            _channel.BasicPublish(exchange, routingKey, basicProperties: props, body: body);

            // WaitForConfirmsOrDie throws OperationInterruptedException on broker nack or
            // timeout. The OutboxPublisherHostedService catches that, records the failure,
            // and re-tries on the next tick — at-least-once delivery preserved.
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(_options.PublishConfirmTimeoutSeconds));
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
