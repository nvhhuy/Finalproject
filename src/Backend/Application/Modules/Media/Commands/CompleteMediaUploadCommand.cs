using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media.Common;
using Application.Modules.Media.Dtos;
using Domains.MediaAggregate;

namespace Application.Modules.Media.Commands;

/// <summary>
///     Called by the client once the presigned PUT to the bucket completes. Verifies the original
///     exists, transitions the asset from <see cref="MediaAssetStatus.Pending" /> to
///     <see cref="MediaAssetStatus.Uploaded" />, and raises the integration event the worker consumes.
/// </summary>
public sealed record CompleteMediaUploadCommand(Guid MediaAssetId, Guid CallerUserId)
    : IRequest<Result<MediaAssetDto, Error>>;

public sealed class CompleteMediaUploadCommandValidator : AbstractValidator<CompleteMediaUploadCommand>
{
    public CompleteMediaUploadCommandValidator()
    {
        RuleFor(x => x.MediaAssetId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}

public sealed class CompleteMediaUploadCommandHandler
    : IRequestHandler<CompleteMediaUploadCommand, Result<MediaAssetDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;
    private readonly IUnitOfWork _uow;

    public CompleteMediaUploadCommandHandler(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _repo = repo;
        _storage = storage;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<MediaAssetDto, Error>> Handle(
        CompleteMediaUploadCommand request,
        CancellationToken cancellationToken)
    {
        var asset = await _repo.GetByIdForUpdateAsync(request.MediaAssetId, cancellationToken);
        if (asset is null)
            return new NotFoundError("Media asset not found.");

        if (asset.OwnerUserId != request.CallerUserId)
            return new ForbiddenError("Caller is not the owner of this media asset.");

        // Idempotent: if a retry hits us after the upload was already confirmed, we just return the
        // current state without re-raising the integration event.
        if (asset.Status != MediaAssetStatus.Pending)
            return MediaAssetProjector.ToDto(asset, _storage);

        // Fetch authoritative metadata (size + content-type as bucket sees it). A null result means
        // the bucket has no object at the expected key — either the client never PUT the bytes
        // or used the wrong URL. Surface a 400 instead of moving to Uploaded and triggering a
        // worker run that would immediately fail.
        var meta = await _storage.GetMetadataAsync(asset.OriginalKey, cancellationToken);
        if (meta is null)
            return new Error("MediaUploadNotFound", 400, "The uploaded object was not found in storage.");

        // Magic-byte content-type verification. The client declared a type at initiate-time which
        // was used to authorise the presigned URL; if the actual bytes don't match a supported
        // media format we reject before raising the integration event. This stops the common
        // "declare image/jpeg, upload .exe" abuse vector. The probe streams only the first
        // 32 bytes — cheap regardless of file size.
        var sniffError = await VerifyContentTypeAsync(asset, cancellationToken);
        if (sniffError is not null)
            return sniffError;

        asset.MarkUploaded(_clock.Now, meta.ByteSize);
        await _uow.SaveChangesAsync(cancellationToken);

        return MediaAssetProjector.ToDto(asset, _storage);
    }

    private async Task<Error?> VerifyContentTypeAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        var buffer = new byte[MediaContentTypeVerifier.RequiredHeaderBytes];
        int bytesRead;
        await using (var payload = await _storage.GetAsync(asset.OriginalKey, cancellationToken))
        {
            bytesRead = await payload.ReadAtLeastAsync(buffer, buffer.Length, false, cancellationToken);
        }

        // A non-empty file shorter than 32 bytes can't be valid media — JPEG SOI alone is 3 bytes
        // but a real photo is always larger. Reject early instead of letting the worker choke.
        if (bytesRead < 8)
            return new Error("MediaContentTypeMismatch", 400,
                "Uploaded payload is too small to be a valid media file.");

        var detected = MediaContentTypeVerifier.Detect(buffer.AsSpan(0, bytesRead));
        if (detected is null)
            return new Error(
                "MediaContentTypeMismatch",
                400,
                $"Uploaded payload was not recognised as a supported media format. Declared content type was '{asset.ContentType}'.");

        if (!MediaContentTypeVerifier.IsCompatible(asset.ContentType, detected))
            return new Error(
                "MediaContentTypeMismatch",
                400,
                $"Declared content type '{asset.ContentType}' does not match the actual payload ('{detected}').");

        return null;
    }
}