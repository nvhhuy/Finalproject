using Domains.MediaAggregate;
using Domains.MediaAggregate.Events;
using Domains.MediaAggregate.ValueObjects;
using Domains.Tenancy;

namespace Domains.Tests.MediaAggregate;

/// <summary>
/// Unit tests for the <see cref="MediaAsset"/> aggregate root. Exercises factory guards, the
/// Pending → Uploaded → Processing → Ready transitions and the failure path, plus invariants on
/// variant attachment.
/// </summary>
public sealed class MediaAssetTests
{
    private static readonly Guid TenantId = SystemTenants.DefaultTenantId;
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string Key = "media/abc/original.jpg";
    private const string ContentType = "image/jpeg";

    [Fact]
    public void CreateForUpload_throws_when_owner_is_empty()
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.CreateForUpload(TenantId, Guid.Empty, MediaKind.Image, Key, ContentType, 100, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForUpload_throws_when_key_is_blank()
    {
        Assert.Throws<ArgumentException>(() =>
            MediaAsset.CreateForUpload(TenantId, Owner, MediaKind.Image, "  ", ContentType, 100, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForUpload_throws_when_byte_size_is_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MediaAsset.CreateForUpload(TenantId, Owner, MediaKind.Image, Key, ContentType, 0, DateTime.UtcNow));
    }

    [Fact]
    public void CreateForUpload_initialises_pending_state()
    {
        DateTime now = DateTime.UtcNow;
        MediaAsset asset = MediaAsset.CreateForUpload(TenantId, Owner, MediaKind.Image, Key, ContentType, 1024, now);

        Assert.Equal(MediaAssetStatus.Pending, asset.Status);
        Assert.Equal(now, asset.CreatedAtUtc);
        Assert.Null(asset.UploadedAtUtc);
        Assert.Empty(asset.Variants);
    }

    [Fact]
    public void MarkUploaded_transitions_state_and_raises_integration_event()
    {
        MediaAsset asset = Pending();
        DateTime uploadedAt = new(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc);

        asset.MarkUploaded(uploadedAt);

        Assert.Equal(MediaAssetStatus.Uploaded, asset.Status);
        Assert.Equal(uploadedAt, asset.UploadedAtUtc);

        MediaAssetUploadedDomainEvent ev = Assert.Single(asset.DomainEvents.OfType<MediaAssetUploadedDomainEvent>());
        Assert.Equal(asset.Id.Value, ev.MediaAssetId);
        Assert.Equal("media.uploaded.v1", ev.RoutingKey);
    }

    [Fact]
    public void MarkUploaded_rejects_non_pending_state()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => asset.MarkUploaded(DateTime.UtcNow));
    }

    [Fact]
    public void MarkProcessing_requires_uploaded_or_failed()
    {
        MediaAsset asset = Pending();
        Assert.Throws<InvalidOperationException>(() => asset.MarkProcessing(DateTime.UtcNow));
    }

    [Fact]
    public void MarkProcessing_records_started_at_for_stale_detection()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);

        DateTime startedAt = new(2026, 5, 26, 14, 0, 0, DateTimeKind.Utc);
        asset.MarkProcessing(startedAt);

        Assert.Equal(startedAt, asset.ProcessingStartedAtUtc);
    }

    [Fact]
    public void RequeueAfterStall_clears_variants_and_re_emits_uploaded_event()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);
        asset.MarkProcessing(DateTime.UtcNow);
        asset.AddVariant(MediaVariantKind.ImageThumbnail, "thumb-partial.jpg", "image/jpeg", 1024, 96, 96);
        asset.ClearDomainEvents();

        DateTime requeuedAt = new(2026, 5, 26, 15, 0, 0, DateTimeKind.Utc);
        asset.RequeueAfterStall(requeuedAt, maxAttempts: 5);

        Assert.Equal(MediaAssetStatus.Uploaded, asset.Status);
        Assert.Null(asset.ProcessingStartedAtUtc);
        Assert.Equal(requeuedAt, asset.UploadedAtUtc);
        Assert.Empty(asset.Variants);
        Assert.Contains(asset.DomainEvents, e => e is MediaAssetUploadedDomainEvent);
    }

    [Fact]
    public void RequeueAfterStall_requires_processing_status()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => asset.RequeueAfterStall(DateTime.UtcNow, maxAttempts: 5));
    }

    [Fact]
    public void MarkProcessing_increments_attempt_count_each_call()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);

        asset.MarkProcessing(DateTime.UtcNow);
        Assert.Equal(1, asset.ProcessingAttemptCount);

        asset.RequeueAfterStall(DateTime.UtcNow, maxAttempts: 5);
        asset.MarkProcessing(DateTime.UtcNow);
        Assert.Equal(2, asset.ProcessingAttemptCount);
    }

    [Fact]
    public void RequeueAfterStall_promotes_to_failed_when_attempt_budget_is_exhausted()
    {
        // Simulate 5 unsuccessful stalled-processing cycles; the 5th recovery call must give up.
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);
        for (int i = 0; i < 4; i++)
        {
            asset.MarkProcessing(DateTime.UtcNow);
            asset.RequeueAfterStall(DateTime.UtcNow, maxAttempts: 5);
        }

        asset.MarkProcessing(DateTime.UtcNow); // 5th attempt
        asset.ClearDomainEvents();

        asset.RequeueAfterStall(DateTime.UtcNow, maxAttempts: 5);

        Assert.Equal(MediaAssetStatus.Failed, asset.Status);
        Assert.NotNull(asset.ErrorMessage);
        Assert.Contains("attempts", asset.ErrorMessage!);
        // Crucially: no fresh upload event — we don't want this to cycle through outbox again.
        Assert.DoesNotContain(asset.DomainEvents, e => e is MediaAssetUploadedDomainEvent);
    }

    [Fact]
    public void MarkUploaded_overrides_byte_size_when_verified_value_is_supplied()
    {
        // Client declared 1000 bytes at initiate-time; bucket received 12345. The verified value
        // wins because it's what billing/quota should use.
        MediaAsset asset = Pending();
        Assert.Equal(100, asset.ByteSize); // see Pending() factory

        asset.MarkUploaded(DateTime.UtcNow, verifiedByteSize: 12345);

        Assert.Equal(12345, asset.ByteSize);
    }

    [Fact]
    public void MarkReady_requires_at_least_one_variant()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);
        asset.MarkProcessing(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => asset.MarkReady(DateTime.UtcNow));
    }

    [Fact]
    public void AddVariant_then_MarkReady_completes_the_pipeline()
    {
        MediaAsset asset = Pending();
        asset.MarkUploaded(DateTime.UtcNow);
        asset.MarkProcessing(DateTime.UtcNow);

        asset.AddVariant(MediaVariantKind.ImageThumbnail, "thumb.jpg", "image/jpeg", byteSize: 4096, width: 256, height: 256);

        DateTime readyAt = DateTime.UtcNow;
        asset.MarkReady(readyAt);

        Assert.Equal(MediaAssetStatus.Ready, asset.Status);
        Assert.Equal(readyAt, asset.ProcessedAtUtc);
        Assert.Single(asset.Variants);
        Assert.Contains(asset.DomainEvents, e => e is MediaAssetReadyDomainEvent);
    }

    [Fact]
    public void AddVariant_throws_when_asset_is_still_pending()
    {
        MediaAsset asset = Pending();

        Assert.Throws<InvalidOperationException>(() =>
            asset.AddVariant(MediaVariantKind.ImageThumbnail, "thumb.jpg", "image/jpeg", 100));
    }

    [Fact]
    public void MarkFailed_truncates_long_error_messages()
    {
        MediaAsset asset = Pending();
        string huge = new('x', 5000);

        asset.MarkFailed(huge);

        Assert.Equal(MediaAssetStatus.Failed, asset.Status);
        Assert.NotNull(asset.ErrorMessage);
        Assert.Equal(2000, asset.ErrorMessage!.Length);
    }

    [Fact]
    public void Explicit_id_factory_preserves_supplied_id()
    {
        MediaAssetId id = MediaAssetId.CreateNew();
        MediaAsset asset = MediaAsset.CreateForUpload(id, TenantId, Owner, MediaKind.Image, Key, ContentType, 100, DateTime.UtcNow);

        Assert.Equal(id, asset.Id);
    }

    private static MediaAsset Pending() =>
        MediaAsset.CreateForUpload(TenantId, Owner, MediaKind.Image, Key, ContentType, 100, DateTime.UtcNow);
}
