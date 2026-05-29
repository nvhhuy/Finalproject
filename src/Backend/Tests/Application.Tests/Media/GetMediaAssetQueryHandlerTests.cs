using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Storage;
using Application.Modules.Media.Queries;
using Domains.MediaAggregate;
using Domains.MediaAggregate.ValueObjects;
using Domains.Tenancy;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Media;

/// <summary>
///     Verifies the ownership check returns the same 404 to a non-owner as for a missing id
///     (defence against id enumeration).
/// </summary>
public sealed class GetMediaAssetQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_returns_not_found_for_missing_asset()
    {
        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdReadOnlyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MediaAsset?)null);

        var handler = new GetMediaAssetQueryHandler(repo.Object, new Mock<IObjectStorage>().Object);

        var result = await handler.Handle(
            new GetMediaAssetQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_caller_is_not_owner()
    {
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var asset = MediaAsset.CreateForUpload(
            MediaAssetId.CreateNew(), SystemTenants.DefaultTenantId, owner, MediaKind.Image,
            "media/originals/x.jpg", "image/jpeg", 1024, FixedNow);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdReadOnlyAsync(asset.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var handler = new GetMediaAssetQueryHandler(repo.Object, new Mock<IObjectStorage>().Object);

        var result = await handler.Handle(
            new GetMediaAssetQuery(asset.Id.Value, attacker), CancellationToken.None);

        Assert.True(result.IsFailure);
        // Same shape as the "missing" case — prevents an attacker from distinguishing the two.
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_dto_when_owner_matches()
    {
        var owner = Guid.NewGuid();
        var asset = MediaAsset.CreateForUpload(
            MediaAssetId.CreateNew(), SystemTenants.DefaultTenantId, owner, MediaKind.Image,
            "media/originals/x.jpg", "image/jpeg", 1024, FixedNow);

        var repo = new Mock<IMediaAssetRepository>();
        repo.Setup(r => r.GetByIdReadOnlyAsync(asset.Id.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(asset);

        var storage = new Mock<IObjectStorage>();
        storage.Setup(s => s.GetPublicUrl(It.IsAny<string>())).Returns("https://cdn/x");

        var handler = new GetMediaAssetQueryHandler(repo.Object, storage.Object);

        var result = await handler.Handle(
            new GetMediaAssetQuery(asset.Id.Value, owner), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(asset.Id.Value, result.Value.Id);
    }
}