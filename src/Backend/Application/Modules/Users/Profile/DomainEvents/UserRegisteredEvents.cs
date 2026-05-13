using Domains.Common.Interfaces;
using MediatR;

namespace Application.Modules.Users.Profile.DomainEvents;

/// <summary>Example: runs right after SaveChanges while an ambient transaction is still open.</summary>
public sealed record UserRegisteredInTransactionEvent(Guid UserId, string Email)
    : INotification, ITransactionalDomainEvent;

/// <summary>Example: runs after Commit (or immediately when there was no explicit transaction).</summary>
public sealed record UserRegisteredAfterCommitEvent(Guid UserId)
    : INotification, IAfterCommitDomainEvent;
