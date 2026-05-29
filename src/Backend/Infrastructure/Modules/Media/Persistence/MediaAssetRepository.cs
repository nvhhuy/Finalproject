using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Persistence;
using Domains.MediaAggregate;
using Domains.MediaAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Media.Persistence;

/// <summary>
///     MediaAsset repository that routes read-only queries to <see cref="IApplicationReadDbContext" />
///     (which can be backed by a replica) and write paths to <see cref="ApplicationDbContext" />. The
///     split exists so that a heavy admin/read load (e.g. listing user assets) doesn't contend with
///     transactional writes on the primary instance.
/// </summary>
public sealed class MediaAssetRepository : IMediaAssetRepository
{
    private readonly IApplicationReadDbContext _read;
    private readonly ApplicationDbContext _write;

    public MediaAssetRepository(ApplicationDbContext write, IApplicationReadDbContext read)
    {
        _write = write;
        _read = read;
    }

    public Task<MediaAsset?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Must hit the write context: the caller mutates the aggregate and SaveChanges expects
        // the same change tracker to own it. Routing through _read here would silently produce
        // detached entities that the unit of work cannot persist.
        return _write.MediaAssets
            .Include(a => a.Variants)
            .FirstOrDefaultAsync(a => a.Id == MediaAssetId.Create(id), cancellationToken);
    }

    public Task<MediaAsset?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _read.MediaAssets
            .AsNoTracking()
            .Include(a => a.Variants)
            .FirstOrDefaultAsync(a => a.Id == MediaAssetId.Create(id), cancellationToken);
    }

    public void Add(MediaAsset asset)
    {
        _write.MediaAssets.Add(asset);
    }

    public void Remove(MediaAsset asset)
    {
        _write.MediaAssets.Remove(asset);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetStalledProcessingAsync(
        DateTime processingStartedBefore,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Stall recovery is a write operation — the caller invokes RequeueAfterStall which
        // mutates state — so we go through _write. Order ascending so the oldest stalled work
        // gets requeued first (fair-queue semantics). The Variants include is mandatory so
        // RequeueAfterStall can clear the collection inside the same change-tracking session
        // (otherwise EF Core won't know which rows to delete).
        return await _write.MediaAssets
            .Include(a => a.Variants)
            .Where(a => a.Status == MediaAssetStatus.Processing
                        && a.ProcessingStartedAtUtc != null
                        && a.ProcessingStartedAtUtc < processingStartedBefore)
            .OrderBy(a => a.ProcessingStartedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _read.MediaAssets.AsNoTracking().AnyAsync(a => a.Id == MediaAssetId.Create(id), cancellationToken);
    }
}