namespace Domains.Common.Interfaces;

/// <summary>
/// Marker for cross-service integration events. The persistence layer captures them into the
/// transactional outbox during <c>SaveChanges</c>; the worker drains the outbox and publishes
/// the payload to RabbitMQ. Implementations should be POCO records: only data, no behaviour.
/// </summary>
public interface IIntegrationEvent : IDomainEvent
{
    /// <summary>Stable event id used for idempotent consumers and tracing.</summary>
    Guid EventId { get; }

    /// <summary>When the producing aggregate raised the event.</summary>
    DateTime OccurredOnUtc { get; }

    /// <summary>
    /// Routing key used when publishing. Implementations should return a stable, kebab-case
    /// identifier such as <c>user.registered.v1</c> — never the CLR type name.
    /// </summary>
    string RoutingKey { get; }
}
