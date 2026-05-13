using Application.Contracts.Events;
using Application.Contracts.Transactions;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _db;
    private readonly IAfterCommitDomainEventQueue _afterCommitQueue;
    private readonly IPublisher _publisher;

    public EfUnitOfWork(ApplicationDbContext db, IAfterCommitDomainEventQueue afterCommitQueue, IPublisher publisher)
    {
        _db = db;
        _afterCommitQueue = afterCommitQueue;
        _publisher = publisher;
    }

    public bool HasActiveTransaction => _db.Database.CurrentTransaction is not null;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is not null)
            return;

        await _db.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction? tx = _db.Database.CurrentTransaction;
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
            await tx.DisposeAsync();
        }

        await _afterCommitQueue.DispatchAllAsync(_publisher, cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _afterCommitQueue.Clear();
        IDbContextTransaction? tx = _db.Database.CurrentTransaction;
        if (tx is null)
            return;

        await tx.RollbackAsync(cancellationToken);
        await tx.DisposeAsync();
    }
}
