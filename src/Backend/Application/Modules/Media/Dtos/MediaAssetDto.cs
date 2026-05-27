using Domains.MediaAggregate;

namespace Application.Modules.Media.Dtos;

/// <summary>
/// API-facing projection of <see cref="MediaAsset"/>. Variants carry resolved public URLs so the
/// client never needs to know the bucket layout or CDN configuration.
/// </summary>
public sealed record MediaAssetDto(
    Guid Id,
    Guid OwnerUserId,
    MediaKind Kind,
    MediaAssetStatus Status,
    string ContentType,
    long ByteSize,
    DateTime CreatedAtUtc,
    DateTime? UploadedAtUtc,
    DateTime? ProcessedAtUtc,
    IReadOnlyList<MediaVariantDto> Variants,
    string? ErrorMessage,
    uint RowVersion);

public sealed record MediaVariantDto(
    Guid Id,
    MediaVariantKind Kind,
    string Url,
    string ContentType,
    long ByteSize,
    int? Width,
    int? Height,
    TimeSpan? Duration);
