using Domains.MediaAggregate;

namespace Worker.Media;

/// <summary>
///     Wire shape that the worker decodes for <c>media.uploaded.v1</c>. Mirrors the camelCase JSON the
///     outbox publishes for <see cref="Domains.MediaAggregate.Events.MediaAssetUploadedDomainEvent" />.
/// </summary>
public sealed record MediaUploadedMessage(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid MediaAssetId,
    Guid OwnerUserId,
    MediaKind Kind,
    string OriginalKey,
    string ContentType,
    long ByteSize);