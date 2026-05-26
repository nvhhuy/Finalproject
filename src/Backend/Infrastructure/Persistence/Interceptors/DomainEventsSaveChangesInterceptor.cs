using Application.Contracts.Events;
using Application.Contracts.Services;
using Domains.Common.Interfaces;
using Infrastructure.Messaging.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace Infrastructure.Persistence.Interceptors;

/// <summary>
/// Two-phase domain-event dispatcher:
/// <list type="bullet">
///   <item><see cref="IIntegrationEvent"/> is captured during <c>SavingChangesAsync</c> as an <see cref="OutboxMessage"/>
///   row written in the same transaction as the business state (transactional outbox).</item>
///   <item><see cref="ITransactionalDomainEvent"/> is published immediately after <c>SaveChanges</c> succeeds (still
///   inside any active explicit transaction). Safe for same-aggregate consistency work.</item>
///   <item><see cref="IAfterCommitDomainEvent"/> is queued until <c>IUnitOfWork.CommitAsync</c>; if no transaction is
///   active it dispatches right away.</item>
/// </list>
/// </summary>
public sealed class DomainEventsSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions OutboxJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly IPublisher _publisher;
    private readonly IAfterCommitDomainEventQueue _afterCommitQueue;
    private readonly ICorrelationContext _correlation;

    public DomainEventsSaveChangesInterceptor(
        IPublisher publisher,
        IAfterCommitDomainEventQueue afterCommitQueue,
        ICorrelationContext correlation)
    {
        _publisher = publisher;
        _afterCommitQueue = afterCommitQueue;
        _correlation = correlation;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            CaptureIntegrationEvents(eventData.Context, _correlation.Current);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0 && eventData.Context is not null)
            await DispatchInProcessDomainEventsAsync(eventData.Context, cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static void CaptureIntegrationEvents(DbContext context, string? correlationId)
    {
        List<IHasDomainEvents> carriers = context.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        foreach (IHasDomainEvents carrier in carriers)
        {
            foreach (IDomainEvent ev in carrier.DomainEvents)
            {
                if (ev is not IIntegrationEvent integration)
                    continue;

                string payload = JsonSerializer.Serialize(integration, integration.GetType(), OutboxJsonOptions);
                var row = new OutboxMessage(
                    eventId: integration.EventId,
                    occurredOnUtc: integration.OccurredOnUtc,
                    type: integration.GetType().AssemblyQualifiedName ?? integration.GetType().FullName!,
                    routingKey: integration.RoutingKey,
                    exchange: null,
                    payloadJson: payload,
                    correlationId: correlationId);

                context.Set<OutboxMessage>().Add(row);
            }
        }
    }

    private async Task DispatchInProcessDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
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
                if (ev is IIntegrationEvent)
                    continue; // already captured into the outbox during SavingChanges

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
