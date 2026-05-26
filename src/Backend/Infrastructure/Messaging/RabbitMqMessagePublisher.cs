using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessagePublisher> logger)
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

    public Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        string exchange = string.IsNullOrWhiteSpace(context.TopicOrExchange) ? _options.DefaultExchange : context.TopicOrExchange;
        string routingKey = context.RoutingKey ?? typeof(TMessage).Name;
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        _channel.BasicPublish(exchange, routingKey, basicProperties: null, body: body);
        _logger.LogDebug("Published {Type} to exchange {Exchange} routing {RoutingKey}", typeof(TMessage).Name, exchange, routingKey);
        return Task.CompletedTask;
    }

    public Task PublishRawAsync(ReadOnlyMemory<byte> payload, MessagePublishContext context, CancellationToken cancellationToken = default)
    {
        string exchange = string.IsNullOrWhiteSpace(context.TopicOrExchange) ? _options.DefaultExchange : context.TopicOrExchange;
        string routingKey = context.RoutingKey ?? context.MessageType ?? "outbox";

        _channel.BasicPublish(exchange, routingKey, basicProperties: null, body: payload);
        _logger.LogDebug("Published raw payload ({Bytes}b) to exchange {Exchange} routing {RoutingKey}", payload.Length, exchange, routingKey);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
