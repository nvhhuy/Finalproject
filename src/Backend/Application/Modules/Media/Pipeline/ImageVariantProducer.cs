using Application.Contracts.Media;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;
using Microsoft.Extensions.Logging;

namespace Application.Modules.Media.Pipeline;

/// <summary>
/// Produces every image variant rung (thumbnails + resized) for a single <see cref="MediaAsset"/>.
/// Extracted from <see cref="MediaProcessingPipeline"/> so the orchestrator can stay short and so
/// the image flow can be tested independently from the video / HLS paths.
/// </summary>
/// <remarks>
/// <para><b>Save cadence:</b> we save after each breakpoint completes both formats (JPEG + WebP),
/// not at the end of the whole ladder. This gives clients an incremental experience — the 96 px
/// thumbnail row is durable while the 1440 px resize is still encoding, so polling endpoints can
/// stream rows as they appear.</para>
///
/// <para><b>Stream reuse:</b> we cannot reuse a single source stream across encoders because
/// ImageSharp consumes it during decode (no seeking on arbitrary IObjectStorage streams). Each
/// variant downloads the original anew — fine because object storage caches well and the API
/// is small.</para>
/// </remarks>
internal sealed class ImageVariantProducer
{
    private readonly IObjectStorage _storage;
    private readonly IImageProcessingService _imageProcessing;
    private readonly IUnitOfWork _uow;
    private readonly ILogger _logger;

    public ImageVariantProducer(
        IObjectStorage storage,
        IImageProcessingService imageProcessing,
        IUnitOfWork uow,
        ILogger logger)
    {
        _storage = storage;
        _imageProcessing = imageProcessing;
        _uow = uow;
        _logger = logger;
    }

    public async Task ProduceAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        // Smallest-first ordering: thumbnails (which are smaller and faster) come before resized,
        // and within each group we walk the breakpoints in their array order (already 96 → 256 and
        // 720 → 1440). Combined with the incremental SaveChanges in EmitBreakpointAsync,
        // clients polling the asset see the 96 px thumbnail row almost immediately while the
        // larger sizes are still being encoded.
        foreach (ImageBreakpoint bp in MediaProcessingPipeline.ThumbnailBreakpoints)
        {
            await EmitBreakpointAsync(asset, MediaVariantKind.ImageThumbnail, bp, square: true, cancellationToken);
        }

        foreach (ImageBreakpoint bp in MediaProcessingPipeline.ResizedBreakpoints)
        {
            await EmitBreakpointAsync(asset, MediaVariantKind.ImageResized, bp, square: false, cancellationToken);
        }
    }

    private async Task EmitBreakpointAsync(
        MediaAsset asset,
        MediaVariantKind kind,
        ImageBreakpoint bp,
        bool square,
        CancellationToken cancellationToken)
    {
        // JPEG + WebP per breakpoint. Saving per format would double DB round-trips for negligible
        // UX benefit — both formats of a given breakpoint complete within milliseconds of each
        // other, and clients use <picture>-style negotiation at the breakpoint level not the
        // format level.
        ImageEncoding[] encodings =
        {
            ImageEncoding.Jpeg(bp.JpegQuality),
            ImageEncoding.Webp(bp.WebpQuality),
        };

        foreach (ImageEncoding encoding in encodings)
        {
            await using Stream src = await _storage.GetAsync(asset.OriginalKey, cancellationToken);

            ProcessedImage variant = square
                ? await _imageProcessing.CreateThumbnailAsync(src, bp.SizePx, encoding, cancellationToken)
                : await _imageProcessing.ResizeAsync(src, bp.SizePx, encoding, cancellationToken);

            string key = square
                ? MediaKeyPlanner.ThumbnailKey(asset.OwnerUserId, asset.Id.Value, bp.SizePx, encoding)
                : MediaKeyPlanner.ResizedKey(asset.OwnerUserId, asset.Id.Value, bp.SizePx, encoding);

            await using (variant.Content)
            {
                await _storage.PutAsync(key, variant.Content, variant.ContentType, cancellationToken);
            }
            asset.AddVariant(kind, key, variant.ContentType, variant.ByteSize,
                variant.Width, variant.Height, duration: null);
        }

        // Incremental commit: by the time the next breakpoint starts, the previous one's variants
        // are durable.
        await _uow.SaveChangesAsync(cancellationToken);
    }
}
