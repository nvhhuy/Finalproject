using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public sealed class NullMessagePublisher : IMessagePublisher
{
    private readonly ILogger<NullMessagePublisher> _logger;

    public NullMessagePublisher(ILogger<NullMessagePublisher> logger) => _logger = logger;

    public Task PublishAsync<TMessage>(TMessage message, MessagePublishContext context, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        _logger.LogDebug("Message bus disabled. Would publish {Type} to {Target}", typeof(TMessage).Name, context.TopicOrExchange);
        return Task.CompletedTask;
    }
}
