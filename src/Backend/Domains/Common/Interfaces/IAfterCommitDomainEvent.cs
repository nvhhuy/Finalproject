namespace Domains.Common.Interfaces;

/// <summary>
///     Raised only after the write transaction commits (or immediately after SaveChanges when no ambient
///     transaction). Use for outbox, email, cache invalidation across services.
/// </summary>
public interface IAfterCommitDomainEvent : IDomainEvent
{
}