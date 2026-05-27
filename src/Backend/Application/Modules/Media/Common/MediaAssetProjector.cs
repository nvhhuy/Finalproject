using Application.Contracts.Storage;
using Application.Modules.Media.Dtos;
using Domains.MediaAggregate;

namespace Application.Modules.Media.Common;

/// <summary>
/// Builds the API-facing <see cref="MediaAssetDto"/> from a tracked <see cref="MediaAsset"/>.
/// Resolves variant storage keys into public/CDN URLs via <see cref="IObjectStorage"/>.
/// </summary>
internal static class MediaAssetProjector
{
    public static MediaAssetDto ToDto(MediaAsset asset, IObjectStorage storage)
    {
        IReadOnlyList<MediaVariantDto> variants = asset.Variants
            .Select(v => new MediaVariantDto(
                v.Id,
                v.Kind,
                storage.GetPublicUrl(v.StorageKey),
                v.ContentType,
                v.ByteSize,
                v.Width,
                v.Height,
                v.Duration))
            .ToList();

        return new MediaAssetDto(
            asset.Id.Value,
            asset.OwnerUserId,
            asset.Kind,
            asset.Status,
            asset.ContentType,
            asset.ByteSize,
            asset.CreatedAtUtc,
            asset.UploadedAtUtc,
            asset.ProcessedAtUtc,
            variants,
            asset.ErrorMessage,
            asset.RowVersion);
    }
}
