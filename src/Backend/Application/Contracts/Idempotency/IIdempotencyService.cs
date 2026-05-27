namespace Application.Contracts.Idempotency;

/// <summary>
/// Stores idempotency keys for mutating HTTP requests so retries return the original response
/// instead of re-executing side effects.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to claim an idempotency key for the given scope and owner partition.
    /// </summary>
    Task<IdempotencyAcquireResult> TryAcquireAsync(
        string scope,
        string ownerPartition,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Persists the successful HTTP response for a claimed key.</summary>
    Task CompleteAsync(
        Guid recordId,
        int responseStatusCode,
        string responseBody,
        CancellationToken cancellationToken = default);

    /// <summary>Releases a claimed key when the handler failed so the client may retry.</summary>
    Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken = default);
}

public enum IdempotencyAcquireStatus
{
    Acquired,
    Replay,
    InProgress
}

public sealed record IdempotencyAcquireResult(
    IdempotencyAcquireStatus Status,
    Guid? RecordId = null,
    int? ResponseStatusCode = null,
    string? ResponseBody = null);
