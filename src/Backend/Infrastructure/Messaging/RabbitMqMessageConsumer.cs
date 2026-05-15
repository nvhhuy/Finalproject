using Application.Contracts.Messaging;
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
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqMessageConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessageConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.DefaultExchange, ExchangeType.Topic, durable: true);
    }

    public Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionName,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        _channel.QueueDeclare(subscriptionName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(subscriptionName, _options.DefaultExchange, subscriptionName);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
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
                _logger.LogError(ex, "Failed to handle message on queue {Queue}", subscriptionName);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(subscriptionName, autoAck: false, consumer);
        _logger.LogInformation("RabbitMQ consumer subscribed to queue {Queue} for {MessageType}", subscriptionName, typeof(TMessage).Name);

        return Task.FromResult<IDisposable>(new SubscriptionHandle(_channel, subscriptionName));
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }

    private sealed class SubscriptionHandle : IDisposable
    {
        private readonly IModel _channel;
        private readonly string _queue;

        public SubscriptionHandle(IModel channel, string queue)
        {
            _channel = channel;
            _queue = queue;
        }

        public void Dispose() => _channel.QueueDelete(_queue);
    }
}
