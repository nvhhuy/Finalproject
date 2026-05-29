namespace Domains.Common.Interfaces;

/// <summary>
///     Raised while the database transaction is still open (after SaveChanges, before Commit). Safe for
///     same-aggregate consistency work.
/// </summary>
public interface ITransactionalDomainEvent : IDomainEvent
{
}