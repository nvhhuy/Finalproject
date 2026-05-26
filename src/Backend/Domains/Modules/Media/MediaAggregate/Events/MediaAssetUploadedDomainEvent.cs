using Domains.Common.Interfaces;

namespace Domains.MediaAggregate.Events;

/// <summary>
/// Integration event raised by <see cref="MediaAsset.MarkUploaded"/>. The persistence interceptor
/// writes this to the transactional outbox in the same transaction as the asset state change, so
/// the media-processing worker picks it up over RabbitMQ at-least-once even if the API host
/// crashes right after commit.
/// </summary>
public sealed record MediaAssetUploadedDomainEvent : IIntegrationEvent
{
    public MediaAssetUploadedDomainEvent(
        Guid mediaAssetId,
        Guid ownerUserId,
        MediaKind kind,
        string originalKey,
        string contentType,
        long byteSize)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        MediaAssetId = mediaAssetId;
        OwnerUserId = ownerUserId;
        Kind = kind;
        OriginalKey = originalKey;
        ContentType = contentType;
        ByteSize = byteSize;
    }

    public Guid EventId { get; init; }
    public DateTime OccurredOnUtc { get; init; }

    /// <summary>Routing key the worker subscribes to.</summary>
    public string RoutingKey => "media.uploaded.v1";

    public Guid MediaAssetId { get; init; }
    public Guid OwnerUserId { get; init; }
    public MediaKind Kind { get; init; }
    public string OriginalKey { get; init; } = null!;
    public string ContentType { get; init; } = null!;
    public long ByteSize { get; init; }
}
