using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Storage;
using Domains.MediaAggregate;

namespace Application.Modules.Media.Queries;

public sealed record StreamMediaAssetQuery(Guid MediaAssetId, Guid CallerUserId)
    : IRequest<Result<StreamMediaAssetResult, Error>>;

public sealed record StreamMediaAssetResult(Stream Stream, string ContentType, bool LeaveStreamOpen = false);

public sealed class StreamMediaAssetQueryHandler
    : IRequestHandler<StreamMediaAssetQuery, Result<StreamMediaAssetResult, Error>>
{
    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;

    public StreamMediaAssetQueryHandler(IMediaAssetRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<Result<StreamMediaAssetResult, Error>> Handle(
        StreamMediaAssetQuery request,
        CancellationToken cancellationToken)
    {
        var asset = await _repo.GetByIdReadOnlyAsync(request.MediaAssetId, cancellationToken);
        if (asset is null || asset.OwnerUserId != request.CallerUserId)
            return new NotFoundError("Media asset not found.");

        if (asset.Status is MediaAssetStatus.Pending or MediaAssetStatus.Failed)
            return new NotFoundError("Media asset not found.");

        var variant = asset.Variants
            .Where(v => v.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.Width ?? int.MaxValue)
            .FirstOrDefault();

        var key = variant?.StorageKey ?? asset.OriginalKey;
        var contentType = variant?.ContentType ?? asset.ContentType;

        try
        {
            var stream = await _storage.GetAsync(key, cancellationToken);
            return new StreamMediaAssetResult(stream, contentType);
        }
        catch
        {
            return new NotFoundError("Media asset not found.");
        }
    }
}
