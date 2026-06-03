using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Storage;
using Application.Mapping;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;

namespace Application.Modules.Users.Profile.Queries;

public sealed record GetProfileQuery(Guid UserId) : IRequest<Result<UserProfileDto, Error>>;

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool TwoFactorEnabled,
    DateTime? EmailVerifiedAtUtc,
    Guid? AvatarMediaAssetId,
    string? AvatarUrl,
    uint RowVersion);

public sealed class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<UserProfileDto, Error>>
{
    private readonly IMediaAssetRepository _media;
    private readonly IObjectStorage _storage;
    private readonly IUserReadRepository _users;

    public GetProfileQueryHandler(IUserReadRepository users, IMediaAssetRepository media, IObjectStorage storage)
    {
        _users = users;
        _media = media;
        _storage = storage;
    }

    public async Task<Result<UserProfileDto, Error>> Handle(GetProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken: cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        var dto = user.ToProfileDto();
        if (user.AvatarMediaAssetId is null)
            return dto with { AvatarUrl = null };

        var asset = await _media.GetByIdReadOnlyAsync(user.AvatarMediaAssetId.Value, cancellationToken);
        if (asset is null || asset.Status == MediaAssetStatus.Failed)
            return dto with { AvatarUrl = null };

        var projected = MediaAssetProjector.ToDto(asset, _storage);
        var avatarUrl = projected.Variants
            .OrderBy(v => v.Width ?? int.MaxValue)
            .FirstOrDefault()
            ?.Url;

        // While the worker processes variants, fall back to the uploaded original so the UI can update immediately.
        if (string.IsNullOrEmpty(avatarUrl)
            && asset.Status is MediaAssetStatus.Uploaded or MediaAssetStatus.Processing or MediaAssetStatus.Ready)
            avatarUrl = _storage.GetPublicUrl(asset.OriginalKey);

        return dto with { AvatarUrl = avatarUrl };
    }
}