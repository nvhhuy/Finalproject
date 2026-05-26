using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media;
using Application.Modules.Media.Commands;
using Domains.MediaAggregate;
using Microsoft.Extensions.Options;
using Moq;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Tests.Media;

/// <summary>
/// Tests for upload initiation: the size cap rejection path and the happy path that reserves the
/// asset, persists it, and issues a presigned URL.
/// </summary>
public sealed class InitiateMediaUploadCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_returns_validation_error_when_byte_size_exceeds_cap()
    {
        var repo = new Mock<IMediaAssetRepository>();
        var storage = new Mock<IObjectStorage>();
        InitiateMediaUploadCommandHandler handler = BuildHandler(repo.Object, storage.Object, maxBytes: 10);

        Result<InitiateMediaUploadResult, Error> result = await handler.Handle(
            new InitiateMediaUploadCommand(Guid.NewGuid(), "image/jpeg", ByteSize: 11, FileExtension: "jpg"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
        repo.Verify(r => r.Add(It.IsAny<MediaAsset>()), Times.Never);
        storage.Verify(s => s.GeneratePresignedUploadUrlAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_reserves_asset_and_returns_presigned_url_on_success()
    {
        var repo = new Mock<IMediaAssetRepository>();
        MediaAsset? added = null;
        repo.Setup(r => r.Add(It.IsAny<MediaAsset>())).Callback<MediaAsset>(a => added = a);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GeneratePresignedUploadUrlAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://bucket/presigned-PUT");

        var uow = new Mock<IUnitOfWork>();
        InitiateMediaUploadCommandHandler handler = BuildHandler(repo.Object, storage.Object, uow: uow.Object);

        Result<InitiateMediaUploadResult, Error> result = await handler.Handle(
            new InitiateMediaUploadCommand(Guid.NewGuid(), "video/mp4", ByteSize: 1024, FileExtension: "mp4"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://bucket/presigned-PUT", result.Value.UploadUrl);
        Assert.NotNull(added);
        Assert.Equal(MediaKind.Video, added!.Kind);
        Assert.Equal(MediaAssetStatus.Pending, added.Status);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static InitiateMediaUploadCommandHandler BuildHandler(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IUnitOfWork? uow = null,
        long maxBytes = 100_000_000)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(FixedNow);

        var opts = Options.Create(new MediaUploadOptions
        {
            MaxUploadBytes = maxBytes,
            PresignedUrlExpirationMinutes = 10,
        });

        return new InitiateMediaUploadCommandHandler(
            repo, storage, uow ?? Mock.Of<IUnitOfWork>(), clock.Object, opts);
    }
}
