namespace Application.Contracts.Media;

/// <summary>
///     High-level orchestration for one media asset. Implementations download the original, dispatch
///     to the image or video processor, write variants to the bucket, and update the aggregate.
/// </summary>
/// <remarks>
///     This sits in the application layer so both the synchronous API path (for tests / dev) and the
///     asynchronous worker (consuming the outbox queue) call the same code path. The implementation
///     lives in <c>Application.Modules.Media.Pipeline.MediaProcessingPipeline</c>.
/// </remarks>
public interface IMediaProcessingPipeline
{
    Task ProcessAsync(Guid mediaAssetId, CancellationToken cancellationToken = default);
}