using Application.Contracts.Events;
using Domains.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.Interceptors;

/// <summary>
/// Publishes <see cref="ITransactionalDomainEvent"/> immediately after SaveChanges (still inside an explicit transaction if one is open).
/// Queues <see cref="IAfterCommitDomainEvent"/> until <see cref="Application.Contracts.Transactions.IUnitOfWork.CommitAsync"/> when a transaction is active; otherwise publishes right away.
/// </summary>
public sealed class DomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;
    private readonly IAfterCommitDomainEventQueue _afterCommitQueue;

    public DomainEventsSaveChangesInterceptor(IPublisher publisher, IAfterCommitDomainEventQueue afterCommitQueue)
    {
        _publisher = publisher;
        _afterCommitQueue = afterCommitQueue;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0 && eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        List<IHasDomainEvents> withEvents = context.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        foreach (IHasDomainEvents entity in withEvents)
        {
            List<IDomainEvent> snapshot = entity.DomainEvents.ToList();
            entity.ClearDomainEvents();

            foreach (IDomainEvent ev in snapshot)
            {
                if (ev is not INotification notification)
                    continue;

                switch (ev)
                {
                    case ITransactionalDomainEvent:
                        await _publisher.Publish(notification, cancellationToken);
                        break;
                    case IAfterCommitDomainEvent:
                        if (context.Database.CurrentTransaction is not null)
                            _afterCommitQueue.Enqueue(notification);
                        else
                            await _publisher.Publish(notification, cancellationToken);
                        break;
                }
            }
        }
    }
}
