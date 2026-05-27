using Application.Common;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Domains.MediaAggregate;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Media.Commands;

/// <summary>
/// Removes an asset and every variant from both the bucket and the database. The bucket deletes
/// happen first because a DB-only delete would leak orphaned objects forever; if a bucket delete
/// fails the DB row stays around so an operator can retry.
/// </summary>
public sealed record DeleteMediaAssetCommand(Guid MediaAssetId, Guid CallerUserId, uint ExpectedRowVersion)
    : IRequest<Result<bool, Error>>;

public sealed class DeleteMediaAssetCommandValidator : AbstractValidator<DeleteMediaAssetCommand>
{
    public DeleteMediaAssetCommandValidator()
    {
        RuleFor(x => x.MediaAssetId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}

public sealed class DeleteMediaAssetCommandHandler : IRequestHandler<DeleteMediaAssetCommand, Result<bool, Error>>
{
    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<DeleteMediaAssetCommandHandler> _logger;

    public DeleteMediaAssetCommandHandler(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IUnitOfWork uow,
        ILogger<DeleteMediaAssetCommandHandler> logger)
    {
        _repo = repo;
        _storage = storage;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<bool, Error>> Handle(DeleteMediaAssetCommand request, CancellationToken cancellationToken)
    {
        MediaAsset? asset = await _repo.GetByIdForUpdateAsync(request.MediaAssetId, cancellationToken);
        if (asset is null)
            return new NotFoundError("Media asset not found.");

        if (asset.OwnerUserId != request.CallerUserId)
            return new ForbiddenError("Caller is not the owner of this media asset.");

        Error? conflict = EntityConcurrency.ValidateExpectedVersion(asset, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        foreach (string key in asset.Variants.Select(v => v.StorageKey).Append(asset.OriginalKey))
        {
            try
            {
                await _storage.DeleteAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete object {Key} for asset {AssetId}; row stays for operator retry", key, asset.Id.Value);
                return new Error("MediaDeleteStorageFailed", 500, "Storage delete failed; please retry.");
            }
        }

        _repo.Remove(asset);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
