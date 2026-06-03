using Application.Common;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Domains.MediaAggregate;
using Shared.Errors.General;

namespace Application.Modules.Users.Profile.Commands;

public sealed record SetMyAvatarCommand(Guid UserId, Guid MediaAssetId, uint ExpectedRowVersion)
    : IRequest<Result<bool, Error>>;

public sealed class SetMyAvatarCommandHandler : IRequestHandler<SetMyAvatarCommand, Result<bool, Error>>
{
    private readonly IMediaAssetRepository _media;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _users;

    public SetMyAvatarCommandHandler(IUserWriteRepository users, IMediaAssetRepository media, IUnitOfWork uow)
    {
        _users = users;
        _media = media;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(SetMyAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdForUpdateAsync(request.UserId, cancellationToken);
        if (user is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(user, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        var asset = await _media.GetByIdForUpdateAsync(request.MediaAssetId, cancellationToken);
        if (asset is null)
            return new NotFoundError("Media asset was not found.");
        if (asset.OwnerUserId != request.UserId)
            return new ForbiddenError("You can only use your own uploads as an avatar.");
        if (asset.Status is not (MediaAssetStatus.Ready or MediaAssetStatus.Uploaded or MediaAssetStatus.Processing))
            return new ValidationError("mediaAssetId", "Avatar image is not ready yet. Try again in a moment.");

        user.SetAvatarMediaAsset(request.MediaAssetId);
        await _uow.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
        return true;
    }
}
