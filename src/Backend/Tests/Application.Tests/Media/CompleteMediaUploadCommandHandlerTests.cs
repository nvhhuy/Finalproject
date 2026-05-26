using System.Text;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media.Commands;
using Application.Modules.Media.Dtos;
using Domains.MediaAggregate;
using Domains.MediaAggregate.ValueObjects;
using Moq;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Tests.Media;

public sealed class CompleteMediaUploadCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();

    [Fact]
    public async Task Handle_returns_not_found_when_asset_missing()
    {
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset?)null);

        CompleteMediaUploadCommandHandler handler = Build(repo.Object, new Mock<IObjectStorage>().Object);

        Result<MediaAssetDto, Error> result = await handler.Handle(
            new CompleteMediaUploadCommand(Guid.NewGuid(), OwnerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_forbidden_when_caller_is_not_owner()
    {
        MediaAsset asset = MediaAsset.CreateForUpload(
            MediaAssetId.CreateNew(), OwnerId, MediaKind.Image,
            "media/originals/x.jpg", "image/jpeg", 1024, FixedNow);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        CompleteMediaUploadCommandHandler handler = Build(repo.Object, new Mock<IObjectStorage>().Object);

        Result<MediaAssetDto, Error> result = await handler.Handle(
            new CompleteMediaUploadCommand(asset.Id.Value, OtherId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task Handle_rejects_when_uploaded_object_is_missing_in_storage()
    {
        MediaAsset asset = MediaAsset.CreateForUpload(
            MediaAssetId.CreateNew(), OwnerId, MediaKind.Image,
            "media/originals/x.jpg", "image/jpeg", 1024, FixedNow);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetMetadataAsync(asset.OriginalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObjectMetadata?)null);

        CompleteMediaUploadCommandHandler handler = Build(repo.Object, storage.Object);

        Result<MediaAssetDto, Error> result = await handler.Handle(
            new CompleteMediaUploadCommand(asset.Id.Value, OwnerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MediaUploadNotFound", result.Error.ErrorType);
    }

    [Fact]
    public async Task Handle_rejects_when_magic_bytes_do_not_match()
    {
        MediaAsset asset = MediaAsset.CreateForUpload(
            MediaAssetId.CreateNew(), OwnerId, MediaKind.Image,
            "media/originals/x.jpg", "image/jpeg", 1024, FixedNow);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(asset.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetMetadataAsync(asset.OriginalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectMetadata(1024, "image/jpeg"));
        // Random bytes that don't match any supported magic-byte signature.
        byte[] randomBytes = new byte[] { 0x4d, 0x5a, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        storage.Setup(s => s.GetAsync(asset.OriginalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => (Stream)new MemoryStream(randomBytes));

        CompleteMediaUploadCommandHandler handler = Build(repo.Object, storage.Object);

        Result<MediaAssetDto, Error> result = await handler.Handle(
            new CompleteMediaUploadCommand(asset.Id.Value, OwnerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("MediaContentTypeMismatch", result.Error.ErrorType);
    }

    private static CompleteMediaUploadCommandHandler Build(IMediaAssetRepository repo, IObjectStorage storage)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(FixedNow);
        return new CompleteMediaUploadCommandHandler(
            repo, storage, Mock.Of<IUnitOfWork>(), clock.Object);
    }
}
