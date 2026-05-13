using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

/// <summary>
/// No-op consumer so Worker can run queue-based services before a real broker adapter is wired.
/// </summary>
public sealed class NullMessageConsumer : IMessageConsumer
{
    private readonly ILogger<NullMessageConsumer> _logger;

    public NullMessageConsumer(ILogger<NullMessageConsumer> logger) => _logger = logger;

    public Task<IDisposable> SubscribeAsync<TMessage>(
        string subscriptionName,
        Func<TMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default) where TMessage : class
    {
        _logger.LogInformation(
            "Message broker disabled. Subscription '{Subscription}' for '{MessageType}' is a no-op.",
            subscriptionName,
            typeof(TMessage).Name);
        return Task.FromResult<IDisposable>(NoopSubscription.Instance);
    }

    private sealed class NoopSubscription : IDisposable
    {
        public static NoopSubscription Instance { get; } = new();
        public void Dispose() { }
    }
}
