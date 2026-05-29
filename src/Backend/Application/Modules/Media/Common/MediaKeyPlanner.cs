using Application.Contracts.Media;
using Domains.MediaAggregate;

namespace Application.Modules.Media.Common;

/// <summary>
///     Single source of truth for the bucket key layout. Centralising the format lets us evolve it
///     later (e.g. add a year/month shard) without hunting through handlers and the worker pipeline.
/// </summary>
/// <remarks>
///     Layout:
///     <code>
///   media/{ownerUserId}/{mediaAssetId}/original{extension}
///   media/{ownerUserId}/{mediaAssetId}/thumb-{size}.{jpg|webp}
///   media/{ownerUserId}/{mediaAssetId}/resized-{maxEdge}.{jpg|webp}
///   media/{ownerUserId}/{mediaAssetId}/poster.jpg
///   media/{ownerUserId}/{mediaAssetId}/video-{height}p.mp4         (default H.264)
///   media/{ownerUserId}/{mediaAssetId}/video-{height}p.hevc.mp4    (HEVC variant)
///   media/{ownerUserId}/{mediaAssetId}/video-{height}p.av1.mp4     (AV1 variant)
///   media/{ownerUserId}/{mediaAssetId}/hls/master.m3u8             (HLS master)
///   media/{ownerUserId}/{mediaAssetId}/hls/{height}p/index.m3u8    (HLS media playlist)
///   media/{ownerUserId}/{mediaAssetId}/hls/{height}p/seg-N.ts      (HLS segment)
/// </code>
///     The prefix <c>media/{ownerUserId}/{mediaAssetId}/</c> is shared by every artefact of one asset
///     — this is what the orphan cleanup job lists when an asset is deleted.
/// </remarks>
public static class MediaKeyPlanner
{
    public static string AssetPrefix(Guid ownerUserId, Guid mediaAssetId)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/";
    }

    public static string OriginalKey(Guid ownerUserId, Guid mediaAssetId, string? extension)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/original{NormalizeExtension(extension)}";
    }

    public static string ThumbnailKey(Guid ownerUserId, Guid mediaAssetId, int sizePx, ImageEncoding encoding)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/thumb-{sizePx}.{encoding.FileExtension}";
    }

    public static string ResizedKey(Guid ownerUserId, Guid mediaAssetId, int maxEdgePx, ImageEncoding encoding)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/resized-{maxEdgePx}.{encoding.FileExtension}";
    }

    public static string PosterKey(Guid ownerUserId, Guid mediaAssetId)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/poster.jpg";
    }

    /// <summary>
    ///     Storage key for a transcoded MP4 variant. The codec is encoded into the filename so the
    ///     orphan-cleanup job and the projection layer can identify variants by URL without consulting
    ///     the database.
    /// </summary>
    public static string VideoVariantKey(Guid ownerUserId, Guid mediaAssetId, int maxHeightPx,
        VideoCodecKind codec = VideoCodecKind.H264)
    {
        var suffix = codec switch
        {
            VideoCodecKind.H264 => string.Empty,
            VideoCodecKind.H265 => ".hevc",
            VideoCodecKind.Av1 => ".av1",
            _ => string.Empty
        };
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/video-{maxHeightPx}p{suffix}.mp4";
    }

    /// <summary>HLS master playlist (multivariant). The single entry-point a player downloads first.</summary>
    public static string HlsMasterKey(Guid ownerUserId, Guid mediaAssetId)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/hls/master.m3u8";
    }

    /// <summary>HLS media playlist for one rung. Listed by the master and contains segment URIs.</summary>
    public static string HlsMediaPlaylistKey(Guid ownerUserId, Guid mediaAssetId, int heightPx)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/hls/{heightPx}p/index.m3u8";
    }

    /// <summary>Per-rung HLS segment directory prefix. FFmpeg writes <c>seg-N.ts</c> files here.</summary>
    public static string HlsSegmentPrefix(Guid ownerUserId, Guid mediaAssetId, int heightPx)
    {
        return $"media/{ownerUserId:N}/{mediaAssetId:N}/hls/{heightPx}p/";
    }

    public static MediaKind KindFromContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type required.", nameof(contentType));

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return MediaKind.Image;
        if (contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return MediaKind.Video;

        throw new ArgumentException($"Unsupported media content type '{contentType}'.", nameof(contentType));
    }

    /// <summary>
    ///     Returns the canonical file extension for a declared content type, or <c>null</c> if the
    ///     content type is not on the supported allowlist. Used to override caller-supplied
    ///     extensions during initiate-upload so an attacker who declares <c>image/jpeg</c> but
    ///     supplies <c>.exe</c> cannot land an executable filename in the bucket (the original key
    ///     still gets the safe extension; magic-byte verification at completion still applies).
    /// </summary>
    public static string? CanonicalExtensionFor(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return null;

        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/heic" => ".heic",
            "video/mp4" => ".mp4",
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            _ => null
        };
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        // Defense-in-depth: trim to the actual extension component (the user might pass a path),
        // strip suspicious characters and reject anything longer than a sane bound. The caller is
        // expected to have already coerced the extension via `CanonicalExtensionFor` for known
        // content types, but this path stays robust if the canonical lookup ever returns null.
        var trimmed = extension.StartsWith('.') ? extension[1..] : extension;
        trimmed = trimmed.ToLowerInvariant();

        // Allow only [a-z0-9]+ extensions so a smuggled "exe;.png" or "../" can't appear inside
        // the storage key.
        if (trimmed.Length == 0 || trimmed.Length > 8)
            return string.Empty;
        foreach (var c in trimmed)
            if (c is not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
                return string.Empty;
        return "." + trimmed;
    }
}