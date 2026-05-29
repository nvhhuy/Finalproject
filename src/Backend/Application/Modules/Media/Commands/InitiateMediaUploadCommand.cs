using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;
using Domains.MediaAggregate.ValueObjects;
using Domains.Tenancy;
using Microsoft.Extensions.Options;

namespace Application.Modules.Media.Commands;

/// <summary>
///     Reserves a new <see cref="MediaAsset" /> row and returns a presigned upload URL the client uses
///     to PUT bytes directly to the bucket. The client then calls
///     <see cref="CompleteMediaUploadCommand" /> once the PUT succeeds. Originals never flow through the
///     API host — only metadata does.
/// </summary>
public sealed record InitiateMediaUploadCommand(
    Guid OwnerUserId,
    string ContentType,
    long ByteSize,
    string? FileExtension) : IRequest<Result<InitiateMediaUploadResult, Error>>;

public sealed record InitiateMediaUploadResult(
    Guid MediaAssetId,
    string UploadUrl,
    string StorageKey,
    DateTime UploadUrlExpiresAtUtc);

public sealed class InitiateMediaUploadCommandValidator : AbstractValidator<InitiateMediaUploadCommand>
{
    public InitiateMediaUploadCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ByteSize)
            .GreaterThan(0)
            .WithMessage("Upload size must be positive.");
    }
}

public sealed class InitiateMediaUploadCommandHandler
    : IRequestHandler<InitiateMediaUploadCommand, Result<InitiateMediaUploadResult, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly MediaUploadOptions _options;
    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;
    private readonly ITenantContext _tenantContext;
    private readonly IUnitOfWork _uow;

    public InitiateMediaUploadCommandHandler(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        ITenantContext tenantContext,
        IOptions<MediaUploadOptions> options)
    {
        _repo = repo;
        _storage = storage;
        _uow = uow;
        _clock = clock;
        _tenantContext = tenantContext;
        _options = options.Value;
    }

    public async Task<Result<InitiateMediaUploadResult, Error>> Handle(
        InitiateMediaUploadCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ByteSize > _options.MaxUploadBytes)
            return new ValidationError(
                nameof(request.ByteSize),
                $"Upload size {request.ByteSize:N0} exceeds the {_options.MaxUploadBytes:N0} byte limit.");

        var kind = MediaKeyPlanner.KindFromContentType(request.ContentType);
        var assetId =
            MediaAssetId.CreateNew();

        // Coerce the storage extension to the canonical one for the declared content type. This
        // means a caller that declares "image/jpeg" can never land "/original.exe" or "/original.sh"
        // in the bucket — the worst case is "/original.jpg" with mismatched bytes, which the
        // magic-byte verification at completion will reject before the asset becomes Ready.
        var canonicalExtension = MediaKeyPlanner.CanonicalExtensionFor(request.ContentType);
        var key = MediaKeyPlanner.OriginalKey(request.OwnerUserId, assetId.Value, canonicalExtension);
        var tenantId = _tenantContext.TenantId ?? SystemTenants.DefaultTenantId;

        var asset = MediaAsset.CreateForUpload(
            assetId,
            tenantId,
            request.OwnerUserId,
            kind,
            key,
            request.ContentType,
            request.ByteSize,
            _clock.Now);

        _repo.Add(asset);
        await _uow.SaveChangesAsync(cancellationToken);

        var expiration = TimeSpan.FromMinutes(_options.PresignedUrlExpirationMinutes);
        var uploadUrl = await _storage.GeneratePresignedUploadUrlAsync(
            key, request.ContentType, expiration, cancellationToken);

        return new InitiateMediaUploadResult(
            assetId.Value,
            uploadUrl,
            key,
            _clock.Now.Add(expiration));
    }
}