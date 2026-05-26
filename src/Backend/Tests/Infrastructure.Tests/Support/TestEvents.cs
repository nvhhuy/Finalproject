using Domains.Common.Interfaces;
using MediatR;

namespace Infrastructure.Tests.Support;

/// <summary>Cross-process event used to exercise the outbox capture path.</summary>
public sealed record TestIntegrationEvent(Guid UserId) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public string RoutingKey => "test.integration.v1";
}

/// <summary>In-process post-commit event (MediatR notification).</summary>
public sealed record TestAfterCommitEvent(Guid UserId) : IAfterCommitDomainEvent, INotification;

/// <summary>In-process transactional event (MediatR notification).</summary>
public sealed record TestTransactionalEvent(Guid UserId) : ITransactionalDomainEvent, INotification;
