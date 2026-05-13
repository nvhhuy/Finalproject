namespace Application.Contracts.Transactions;

/// <summary>
/// Ambient transaction for the scoped write DbContext (EF Core).
/// A single SQL transaction cannot span two PostgreSQL instances; use outbox or sagas when coordinating multiple physical databases.
/// </summary>
public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
