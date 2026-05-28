using Domains.Common.Interfaces;

namespace Application.Contracts.Messaging;

/// <summary>Enqueue integration events to the outbox outside an aggregate SaveChanges cycle.</summary>
public interface IIntegrationEventOutboxWriter
{
    Task EnqueueAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
