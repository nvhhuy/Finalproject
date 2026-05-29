using System.Collections.Concurrent;
using Application.Contracts.Events;
using MediatR;

namespace Infrastructure.Persistence.Events;

public sealed class AfterCommitDomainEventQueue : IAfterCommitDomainEventQueue
{
    private readonly ConcurrentQueue<INotification> _pending = new();

    public void Enqueue(INotification notification)
    {
        _pending.Enqueue(notification);
    }

    public void Clear()
    {
        while (_pending.TryDequeue(out _))
        {
        }
    }

    public async Task DispatchAllAsync(IPublisher publisher, CancellationToken cancellationToken = default)
    {
        while (_pending.TryDequeue(out var notification))
            await publisher.Publish(notification, cancellationToken);
    }
}