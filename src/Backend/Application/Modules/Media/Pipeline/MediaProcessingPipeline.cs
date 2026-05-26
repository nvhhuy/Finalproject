using Application.Contracts.Media;
using Application.Contracts.Modules.Media.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Domains.MediaAggregate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Modules.Media.Pipeline;

/// <summary>
/// Orchestrates the end-to-end transformation of one <see cref="MediaAsset"/>: scans the
/// payload, routes to the appropriate variant producer, and transitions the aggregate to
/// <see cref="MediaAssetStatus.Ready"/> (or <see cref="MediaAssetStatus.Failed"/> with a recorded
/// error). The actual encoding work lives in three specialist producers so this file stays a
/// thin policy layer.
/// </summary>
/// <remarks>
/// <para>
/// The same implementation is used by both the in-process API path (rare; useful for tests) and
/// the asynchronous worker that drains the <c>media.uploaded.v1</c> queue. Idempotency: callers
/// can safely invoke <see cref="ProcessAsync"/> multiple times for the same id — assets in any
/// state other than <see cref="MediaAssetStatus.Uploaded"/> or <see cref="MediaAssetStatus.Failed"/>
/// are skipped.
/// </para>
/// <para>
/// <b>Image ladder</b>: 2 thumbnail sizes (96, 256, square centre-crop) and 2 resized sizes
/// (720, 1440, aspect-preserving). Each is emitted in <b>both JPEG and WebP</b> so the client can
/// use a <c>&lt;picture&gt;</c> element to negotiate format. Total: 4 × 2 = <b>8 variants</b> per image.
/// </para>
/// <para>
/// <b>Video ladder</b>: poster JPEG + transcoded MP4 at every rung ≤ source height. Standard
/// rungs are 480p / 720p / 1080p / 1440p (2K) / 2160p (4K); the worker never upsamples.
/// </para>
/// </remarks>
public sealed class MediaProcessingPipeline : IMediaProcessingPipeline
{
    /// <summary>
    /// Square-crop thumbnails — used for avatars, mentions and dense grid views. Quality is lower
    /// than the larger variants because at this scale users can't see compression artefacts.
    /// </summary>
    public static readonly ImageBreakpoint[] ThumbnailBreakpoints =
    {
        new(SizePx: 96,  JpegQuality: 70, WebpQuality: 60),
        new(SizePx: 256, JpegQuality: 78, WebpQuality: 70),
    };

    /// <summary>Aspect-preserving resizes — used for the feed (720) and full-screen / desktop hero (1440).</summary>
    public static readonly ImageBreakpoint[] ResizedBreakpoints =
    {
        new(SizePx: 720,  JpegQuality: 82, WebpQuality: 75),
        new(SizePx: 1440, JpegQuality: 85, WebpQuality: 78),
    };

    /// <summary>
    /// Video transcode ladder, ordered highest-to-lowest. Standard breakpoints follow YouTube /
    /// Netflix conventions: 2160 (4K UHD), 1440 (2K QHD), 1080 (Full HD), 720 (HD), 480 (SD).
    /// </summary>
    public static readonly int[] VideoHeightRungs = { 2160, 1440, 1080, 720, 480 };

    private readonly IMediaAssetRepository _repo;
    private readonly IObjectStorage _storage;
    private readonly IMediaScanner _scanner;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly MediaUploadOptions _options;
    private readonly ILogger<MediaProcessingPipeline> _logger;

    private readonly ImageVariantProducer _imageProducer;
    private readonly VideoVariantProducer _videoProducer;
    private readonly HlsLadderProducer _hlsProducer;

    public MediaProcessingPipeline(
        IMediaAssetRepository repo,
        IObjectStorage storage,
        IImageProcessingService imageProcessing,
        IVideoProcessingService videoProcessing,
        IMediaScanner scanner,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IOptions<MediaUploadOptions> options,
        ILogger<MediaProcessingPipeline> logger)
    {
        _repo = repo;
        _storage = storage;
        _scanner = scanner;
        _uow = uow;
        _clock = clock;
        _options = options.Value;
        _logger = logger;

        // Producers compose this pipeline's dependencies; they're not DI-registered separately
        // because their lifetimes match the pipeline's exactly. Constructing them here keeps the
        // (already long) public constructor signature stable for tests.
        _imageProducer = new ImageVariantProducer(storage, imageProcessing, uow, logger);
        _videoProducer = new VideoVariantProducer(storage, videoProcessing, uow, _options, logger);
        _hlsProducer = new HlsLadderProducer(storage, videoProcessing, uow, _options, logger);
    }

    public async Task ProcessAsync(Guid mediaAssetId, CancellationToken cancellationToken = default)
    {
        MediaAsset? asset = await _repo.GetByIdForUpdateAsync(mediaAssetId, cancellationToken);
        if (asset is null)
        {
            _logger.LogWarning("Media asset {AssetId} disappeared before the worker could pick it up", mediaAssetId);
            return;
        }

        if (asset.Status is not (MediaAssetStatus.Uploaded or MediaAssetStatus.Failed))
        {
            _logger.LogInformation(
                "Skipping media asset {AssetId} because its status is {Status}", mediaAssetId, asset.Status);
            return;
        }

        asset.MarkProcessing(_clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            if (await ShouldQuarantineAsync(asset, cancellationToken))
                return;

            switch (asset.Kind)
            {
                case MediaKind.Image:
                    await _imageProducer.ProduceAsync(asset, cancellationToken);
                    break;
                case MediaKind.Video:
                    int[] rungs = await _videoProducer.ProduceAsync(asset, cancellationToken);
                    if (_options.EnableHls)
                        await _hlsProducer.ProduceAsync(asset, rungs, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported media kind {asset.Kind}.");
            }

            asset.MarkReady(_clock.Now);
            await _uow.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Media asset {AssetId} processed: {VariantCount} variants",
                asset.Id.Value, asset.Variants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Media processing failed for asset {AssetId}", asset.Id.Value);
            asset.MarkFailed(ex.Message);
            await _uow.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Scans the original payload before any encoding work. A <see cref="ScanVerdict.Quarantine"/>
    /// verdict is terminal — the asset transitions to Failed and we return early. Unknown is
    /// logged but we proceed fail-open (operators set <c>MediaScanner:FailClosed</c> on the
    /// scanner itself to invert that policy).
    /// </summary>
    private async Task<bool> ShouldQuarantineAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        await using Stream original = await _storage.GetAsync(asset.OriginalKey, cancellationToken);
        MediaScanResult scan = await _scanner.ScanAsync(original, asset.ContentType, cancellationToken);

        if (scan.Verdict == ScanVerdict.Quarantine)
        {
            _logger.LogError(
                "Quarantined media asset {AssetId} during scan: {Detail}", asset.Id.Value, scan.Detail);
            asset.MarkFailed($"Scanner rejected payload: {scan.Detail}");
            await _uow.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (scan.Verdict == ScanVerdict.Unknown)
        {
            _logger.LogWarning(
                "Scanner returned Unknown for asset {AssetId} ({Detail}); proceeding fail-open",
                asset.Id.Value, scan.Detail);
        }

        return false;
    }
}

/// <summary>
/// A single image rung in the pipeline. Quality is split per codec because WebP needs lower
/// numerical quality to match JPEG perception (~10–15 points lower).
/// </summary>
public sealed record ImageBreakpoint(int SizePx, int JpegQuality, int WebpQuality);
