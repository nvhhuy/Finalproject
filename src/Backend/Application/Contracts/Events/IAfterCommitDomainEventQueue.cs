using MediatR;

namespace Application.Contracts.Events;

/// <summary>Buffers <see cref="IAfterCommitDomainEvent"/> notifications until the write transaction commits.</summary>
public interface IAfterCommitDomainEventQueue
{
    void Enqueue(INotification notification);

    void Clear();

    Task DispatchAllAsync(IPublisher publisher, CancellationToken cancellationToken = default);
}
