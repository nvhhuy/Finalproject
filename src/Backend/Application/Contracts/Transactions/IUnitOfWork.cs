namespace Application.Contracts.Transactions;

/// <summary>
///     Unit of work over the scoped write DbContext (EF Core).
///     <para>
///         Owns <c>SaveChanges</c> for the current request: handlers stage mutations via repositories,
///         then call <see cref="SaveChangesAsync(CancellationToken)" /> exactly once before returning.
///     </para>
///     <para>
///         Also exposes ambient transaction control used by <c>TransactionalActionFilter</c>. A single
///         SQL transaction cannot span two PostgreSQL instances; use the outbox or sagas when coordinating
///         multiple physical databases.
///     </para>
/// </summary>
public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    /// <summary>Persist staged changes on the scoped write DbContext.</summary>
    /// <returns>Number of affected rows, mirroring <c>DbContext.SaveChangesAsync</c>.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}