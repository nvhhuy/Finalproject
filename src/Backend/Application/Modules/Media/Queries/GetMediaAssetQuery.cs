using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Storage;
using Application.Modules.Media.Common;
using Application.Modules.Media.Dtos;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Media.Queries;

/// <summary>
/// Fetch one media asset by id, on behalf of <see cref="CallerUserId"/>. The caller id is the
/// authenticated user pulled from JWT — the handler returns <c>NotFound</c> (not <c>Forbidden</c>)
/// when the asset exists but belongs to someone else, so that enumeration attacks cannot
/// distinguish "no such id" from "not mine".
/// </summary>
public sealed record GetMediaAssetQuery(Guid MediaAssetId, Guid CallerUserId) : IRequest<Result<MediaAssetDto, Error>>;

public sealed class GetMediaAssetQueryHandler : IRequestHandler<GetMediaAssetQuery, Result<MediaAssetDto, Error>>
{
    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;

    public GetMediaAssetQueryHandler(IMediaAssetRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<Result<MediaAssetDto, Error>> Handle(GetMediaAssetQuery request, CancellationToken cancellationToken)
    {
        Domains.MediaAggregate.MediaAsset? asset = await _repo.GetByIdReadOnlyAsync(request.MediaAssetId, cancellationToken);
        if (asset is null)
            return new NotFoundError("Media asset not found.");

        // Critical: same error message whether the asset doesn't exist OR exists but is owned
        // by someone else. Distinguishing these two cases would let an attacker enumerate valid
        // asset ids across the entire system.
        if (asset.OwnerUserId != request.CallerUserId)
            return new NotFoundError("Media asset not found.");

        return MediaAssetProjector.ToDto(asset, _storage);
    }
}
