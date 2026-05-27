using Application.Contracts.Idempotency;
using Application.Contracts.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Idempotency;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IdempotencyOptions _options;

    public IdempotencyService(
        ApplicationDbContext db,
        IDateTimeProvider clock,
        IOptions<IdempotencyOptions> options)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<IdempotencyAcquireResult> TryAcquireAsync(
        string scope,
        string ownerPartition,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        DateTime now = _clock.Now;
        TimeSpan retention = TimeSpan.FromHours(Math.Max(1, _options.RetentionHours));
        TimeSpan inProgressTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.InProgressTimeoutMinutes));

        IdempotencyRecord? existing = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(
                x => x.Scope == scope && x.OwnerPartition == ownerPartition && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.ExpiresAtUtc <= now)
            {
                _db.IdempotencyRecords.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
            else if (existing.Status == IdempotencyRecordStatus.Completed)
            {
                return new IdempotencyAcquireResult(
                    IdempotencyAcquireStatus.Replay,
                    ResponseStatusCode: existing.ResponseStatusCode,
                    ResponseBody: existing.ResponseBody);
            }
            else if (existing.CreatedAtUtc.Add(inProgressTimeout) > now)
            {
                return new IdempotencyAcquireResult(IdempotencyAcquireStatus.InProgress);
            }
            else
            {
                _db.IdempotencyRecords.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        IdempotencyRecord record = IdempotencyRecord.CreateInProgress(scope, ownerPartition, idempotencyKey, now, retention);
        _db.IdempotencyRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return new IdempotencyAcquireResult(IdempotencyAcquireStatus.Acquired, record.Id);
        }
        catch (DbUpdateException)
        {
            IdempotencyRecord? raced = await _db.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Scope == scope && x.OwnerPartition == ownerPartition && x.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (raced?.Status == IdempotencyRecordStatus.Completed)
            {
                return new IdempotencyAcquireResult(
                    IdempotencyAcquireStatus.Replay,
                    ResponseStatusCode: raced.ResponseStatusCode,
                    ResponseBody: raced.ResponseBody);
            }

            return new IdempotencyAcquireResult(IdempotencyAcquireStatus.InProgress);
        }
    }

    public async Task CompleteAsync(
        Guid recordId,
        int responseStatusCode,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        IdempotencyRecord? record = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null)
            return;

        record.Complete(responseStatusCode, responseBody);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        IdempotencyRecord? record = await _db.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null)
            return;

        _db.IdempotencyRecords.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
