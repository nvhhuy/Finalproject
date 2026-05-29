using Application.Contracts.Media;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;
using Microsoft.Extensions.Logging;

namespace Application.Modules.Media.Pipeline;

/// <summary>
///     Produces the MP4 transcode ladder + poster for a single <see cref="MediaAsset" />. Returns the
///     rung heights that were emitted so <see cref="HlsLadderProducer" /> can mirror exactly the same
///     ladder for HLS without re-probing the source.
/// </summary>
/// <remarks>
///     <para>
///         <b>Parallelism:</b> when <see cref="MediaUploadOptions.MaxConcurrentVideoRungs" /> > 1 the
///         rungs encode in parallel. ffmpeg processes are independent but the EF Core <c>DbContext</c> is
///         not thread-safe, so AddVariant + SaveChanges are serialised through a per-call
///         <see cref="SemaphoreSlim" />. Storage PUTs run outside that lock — they're independently safe
///         and we don't want a slow upload blocking other rungs' DB commits.
///     </para>
///     <para>
///         <b>Codec ladder:</b> H.264 is always emitted (universal compat). When
///         <see cref="MediaUploadOptions.AdditionalVideoCodecs" /> lists HEVC / AV1, each rung is also
///         re-encoded in those codecs so cost-sensitive clients can negotiate down to a smaller payload
///         via the MIME <c>codecs=</c> parameter.
///     </para>
/// </remarks>
internal sealed class VideoVariantProducer
{
    private readonly ILogger _logger;
    private readonly MediaUploadOptions _options;
    private readonly IObjectStorage _storage;
    private readonly IUnitOfWork _uow;
    private readonly IVideoProcessingService _videoProcessing;

    public VideoVariantProducer(
        IObjectStorage storage,
        IVideoProcessingService videoProcessing,
        IUnitOfWork uow,
        MediaUploadOptions options,
        ILogger logger)
    {
        _storage = storage;
        _videoProcessing = videoProcessing;
        _uow = uow;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    ///     Encodes every applicable rung for the asset. Returns the ordered set of rung heights so
    ///     callers (HLS) can build a parallel ladder without re-probing.
    /// </summary>
    public async Task<int[]> ProduceAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        VideoProbeResult probe;
        await using (var src = await _storage.GetAsync(asset.OriginalKey, cancellationToken))
        {
            probe = await _videoProcessing.ProbeAsync(src, cancellationToken);
        }

        await EmitPosterAsync(asset, probe, cancellationToken);

        // MP4 ladder: emit every rung ≤ source height (never upsample). When the source is
        // shorter than the lowest rung, emit a single re-encoded copy at native resolution for
        // codec/container normalisation.
        var sourceHeight = probe.Height ?? MediaProcessingPipeline.VideoHeightRungs[0];

        // Smallest-first so the lowest-quality (cheapest, fastest) variant is durable first.
        // For a 4K source that means 480p is playable in <30 s while 4K transcoding still has
        // minutes to go.
        var applicableRungs = MediaProcessingPipeline.VideoHeightRungs
            .Where(r => r <= sourceHeight)
            .OrderBy(r => r)
            .ToArray();
        if (applicableRungs.Length == 0)
            applicableRungs = new[] { sourceHeight };

        var maxParallel = Math.Max(1, _options.MaxConcurrentVideoRungs);
        if (maxParallel == 1)
        {
            foreach (var rung in applicableRungs)
                await EncodeAndPersistRungAsync(asset, rung, null, cancellationToken);
        }
        else
        {
            // dbLock serialises DbContext access; encodeLimit caps concurrent ffmpeg processes
            // so a worker with 4 vCPUs doesn't spawn 8 ffmpegs and thrash itself.
            using var dbLock = new SemaphoreSlim(1, 1);
            using var encodeLimit = new SemaphoreSlim(maxParallel, maxParallel);

            Task[] rungTasks = applicableRungs.Select(async rung =>
            {
                await encodeLimit.WaitAsync(cancellationToken);
                try
                {
                    await EncodeAndPersistRungAsync(asset, rung, dbLock, cancellationToken);
                }
                finally
                {
                    encodeLimit.Release();
                }
            }).ToArray();

            await Task.WhenAll(rungTasks);
        }

        return applicableRungs;
    }

    private async Task EmitPosterAsync(MediaAsset asset, VideoProbeResult probe, CancellationToken cancellationToken)
    {
        // Offset a small amount to avoid the black title cards many editors prepend. Sources
        // shorter than 2 s pick frame zero — they're typically GIF-replaced clips where the
        // first frame is already representative.
        var posterOffset = probe.Duration is { } d && d > TimeSpan.FromSeconds(2)
            ? TimeSpan.FromSeconds(1)
            : TimeSpan.Zero;

        await using var src = await _storage.GetAsync(asset.OriginalKey, cancellationToken);
        var poster = await _videoProcessing.ExtractPosterAsync(src, posterOffset, cancellationToken);
        var posterKey = MediaKeyPlanner.PosterKey(asset.OwnerUserId, asset.Id.Value);

        await using (poster.Content)
        {
            await _storage.PutAsync(posterKey, poster.Content, poster.ContentType, cancellationToken);
        }

        asset.AddVariant(MediaVariantKind.VideoPoster, posterKey, poster.ContentType,
            poster.ByteSize, probe.Width, probe.Height);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task EncodeAndPersistRungAsync(
        MediaAsset asset,
        int rung,
        SemaphoreSlim? dbLock,
        CancellationToken cancellationToken)
    {
        await EncodeAndPersistCodecAsync(asset, rung, VideoCodecKind.H264, dbLock, cancellationToken);

        foreach (var extra in _options.AdditionalVideoCodecs)
        {
            if (extra == VideoCodecKind.H264) continue; // already emitted above
            await EncodeAndPersistCodecAsync(asset, rung, extra, dbLock, cancellationToken);
        }
    }

    private async Task EncodeAndPersistCodecAsync(
        MediaAsset asset,
        int rung,
        VideoCodecKind codec,
        SemaphoreSlim? dbLock,
        CancellationToken cancellationToken)
    {
        await using var src = await _storage.GetAsync(asset.OriginalKey, cancellationToken);
        var encoded = await _videoProcessing.TranscodeToMp4Async(src, rung, codec, cancellationToken);
        var videoKey = MediaKeyPlanner.VideoVariantKey(asset.OwnerUserId, asset.Id.Value, rung, codec);
        var variantKind = codec switch
        {
            VideoCodecKind.H265 => MediaVariantKind.VideoMp4Hevc,
            VideoCodecKind.Av1 => MediaVariantKind.VideoMp4Av1,
            _ => MediaVariantKind.VideoMp4
        };

        // Upload to storage outside the DB lock so a slow S3 PUT doesn't block other rungs.
        await using (encoded.Content)
        {
            await _storage.PutAsync(videoKey, encoded.Content, encoded.ContentType, cancellationToken);
        }

        if (dbLock is not null)
            await dbLock.WaitAsync(cancellationToken);
        try
        {
            asset.AddVariant(variantKind, videoKey, encoded.ContentType,
                encoded.ByteSize, encoded.Width, encoded.Height, encoded.Duration);
            await _uow.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbLock?.Release();
        }
    }
}