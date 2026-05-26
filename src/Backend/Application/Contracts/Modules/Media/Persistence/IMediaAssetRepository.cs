using Domains.MediaAggregate;

namespace Application.Contracts.Modules.Media.Persistence;

/// <summary>
/// Write-side repository for <see cref="MediaAsset"/>. The aggregate is small, the write surface
/// is narrow, and there's no caching, so a single combined read/write repository keeps the layout
/// simpler than the User module's split.
/// </summary>
public interface IMediaAssetRepository
{
    /// <summary>Loads a tracked asset with its variants for state-changing operations.</summary>
    Task<MediaAsset?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads an asset without tracking — useful for read pipelines that don't mutate state.</summary>
    Task<MediaAsset?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(MediaAsset asset);

    void Remove(MediaAsset asset);

    /// <summary>
    /// Returns assets that have been in <see cref="Domains.MediaAggregate.MediaAssetStatus.Processing"/>
    /// since before <paramref name="processingStartedBefore"/>. Used by the stale-recovery cron job
    /// to find wedged work and requeue it.
    /// </summary>
    /// <param name="processingStartedBefore">UTC cutoff — assets that started processing before this instant are considered stale.</param>
    /// <param name="limit">Page size cap so a backlog after an extended outage doesn't pull the entire table into a single transaction.</param>
    Task<IReadOnlyList<MediaAsset>> GetStalledProcessingAsync(
        DateTime processingStartedBefore,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether an asset with the given id exists. Used by the orphan cleanup job to test
    /// candidate storage keys against the database without materialising the asset itself.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
