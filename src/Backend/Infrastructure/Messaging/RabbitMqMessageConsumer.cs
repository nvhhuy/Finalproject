using Application.Contracts.Messaging;
using Application.Contracts.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

public sealed class RabbitMqMessageConsumer : IMessageConsumer, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageConsumer> _logger;
    private readonly ICorrelationContext _correlation;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqMessageConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessageConsumer> logger,
        ICorrelationContext correlation)
    {
        _options = options.Value;
        _logger = logger;
        _correlation = correlation;

        // DispatchConsumersAsync=true is required by RabbitMQ.Client 6.x for AsyncEventingBasicConsumer
        // to actually dispatch handlers asynchronously. Without it the async Received handler
        // runs sync-over-async on the connection's read loop — sub-tly deadlocks under any handler
        // that awaits and yields back to the same loop (e.g. EF Core ExecuteSqlInterpolated).
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

        // BasicQos enforces competing-consumer load balancing. Without it the broker dispatches
        // the entire queue to a single consumer at startup, defeating horizontal scale and
        // risking OOM on long backlogs. prefetchSize=0 means "no byte cap"; prefetchCount caps
        // unacked messages per consumer; global=false makes the limit per-consumer not per-channel.
        _channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);
    }

    public Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionName,
        Func<TMessage, CancellationToken, Task> handler,
        SubscriptionOptions? options = null,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        bool enableDlq = options?.EnableDeadLetterQueue == true;
        DeclareQueue(subscriptionName, enableDlq);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            // Restore the correlation id from the AMQP envelope so log entries inside the handler
            // stitch back to the originating request. Prefer the top-level property; fall back to
            // the `correlation-id` header for messages from publishers that only emit headers.
            string? correlationId = ea.BasicProperties?.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId)
                && ea.BasicProperties?.Headers is { } headers
                && headers.TryGetValue("correlation-id", out object? raw)
                && raw is byte[] bytes)
            {
                correlationId = Encoding.UTF8.GetString(bytes);
            }

            // BeginScope + ICorrelationContext together: BeginScope reaches the log sinks
            // (Serilog reads it via the ILogger scope), ICorrelationContext reaches application
            // code that wants to forward the id onto further outbound messages.
            using IDisposable? loggerScope = !string.IsNullOrWhiteSpace(correlationId)
                ? _logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId })
                : null;
            using IDisposable? ctxScope = !string.IsNullOrWhiteSpace(correlationId)
                ? _correlation.BeginScope(correlationId)
                : null;

            try
            {
                string json = Encoding.UTF8.GetString(ea.Body.Span);
                TMessage? message = JsonSerializer.Deserialize<TMessage>(json);
                if (message is not null)
                    await handler(message, cancellationToken);
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                if (enableDlq)
                {
                    // DLQ mode: drop the message into the dead-letter exchange rather than
                    // requeuing. Retries live at the application layer (stale-recovery + outbox)
                    // so this stops the broker-level nack-loop that poisons the consumer pool.
                    _logger.LogError(ex,
                        "Handler failed on queue {Queue}; routing to DLQ via dead-letter exchange",
                        subscriptionName);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
                else
                {
                    _logger.LogError(ex, "Failed to handle message on queue {Queue}", subscriptionName);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
        };

        string consumerTag = _channel.BasicConsume(subscriptionName, autoAck: false, consumer);
        _logger.LogInformation(
            "RabbitMQ consumer subscribed to queue {Queue} for {MessageType} (dlq={Dlq})",
            subscriptionName, typeof(TMessage).Name, enableDlq);

        return Task.FromResult<IDisposable>(new SubscriptionHandle(_channel, subscriptionName, consumerTag, _logger));
    }

    /// <summary>
    /// Declares the primary queue + (when DLQ is enabled) the dead-letter exchange and dead-letter
    /// queue. Mirrors RabbitMQ's standard DLX pattern: the primary queue carries an
    /// <c>x-dead-letter-exchange</c> argument that points at a topic exchange dedicated to dead-
    /// letters; that exchange has a single bound queue (<c>{name}.dlq</c>) which is durable but
    /// never consumed by the worker — operators inspect it manually.
    /// </summary>
    /// <remarks>
    /// <para><b>Important migration note:</b> RabbitMQ refuses to redeclare an existing queue with
    /// different arguments. If the queue was created before DLQ was enabled, the first run after
    /// upgrade will throw <c>PRECONDITION_FAILED: inequivalent arg 'x-dead-letter-exchange'</c>.
    /// Operators must delete the queue once (the messaging UI or
    /// <c>rabbitmqctl delete_queue {name}</c>); pending messages are lost. For at-least-once
    /// safety, drain the queue before deletion or coordinate with the publishers.</para>
    /// </remarks>
    private void DeclareQueue(string queueName, bool enableDlq)
    {
        if (!enableDlq)
        {
            _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queueName, _options.DefaultExchange, queueName);
            return;
        }

        string dlxName = $"{queueName}.dlx";
        string dlqName = $"{queueName}.dlq";

        // Dead-letter exchange + parking queue. The DLX is topic so future per-failure-reason
        // routing can be added without touching the main flow.
        _channel.ExchangeDeclare(dlxName, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(dlqName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(dlqName, dlxName, routingKey: "#");

        // Primary queue with dead-lettering wired in. The original message body + headers are
        // preserved by the broker when it moves the message to the DLX, including an x-death
        // header that records the rejection history (useful for diagnostics).
        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = queueName,
        };
        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
        _channel.QueueBind(queueName, _options.DefaultExchange, queueName);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Releases the consumer registration without destroying the underlying queue. Cancelling the
    /// consumer leaves any in-flight messages requeued by the broker and preserves messages produced
    /// while the worker is down — critical for restart-resilient at-least-once delivery.
    /// </summary>
    private sealed class SubscriptionHandle : IDisposable
    {
        private readonly IModel _channel;
        private readonly string _queue;
        private readonly string _consumerTag;
        private readonly ILogger _logger;

        public SubscriptionHandle(IModel channel, string queue, string consumerTag, ILogger logger)
        {
            _channel = channel;
            _queue = queue;
            _consumerTag = consumerTag;
            _logger = logger;
        }

        public void Dispose()
        {
            try
            {
                if (_channel.IsOpen)
                    _channel.BasicCancel(_consumerTag);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel RabbitMQ consumer for queue {Queue}", _queue);
            }
        }
    }
}
