using Application.Contracts.Media;

namespace Application.Modules.Media;

/// <summary>Tunables for the media upload flow that the application layer owns.</summary>
/// <remarks>
///     Kept in the Application layer so the upload-initiation handler doesn't need to reach into
///     <c>Infrastructure.Storage.ObjectStorageOptions</c> (which would invert the dependency direction).
///     Bound to <c>Media:Upload</c> by the host.
/// </remarks>
public sealed class MediaUploadOptions
{
    public const string SectionName = "Media:Upload";

    /// <summary>How long the presigned PUT URL remains valid after issuance.</summary>
    public int PresignedUrlExpirationMinutes { get; init; } = 10;

    /// <summary>
    ///     Maximum accepted upload size, in bytes. Defaults to <b>2 GiB</b> — enough to accept short 4K
    ///     clips (a 60 s 4K MP4 is ~300 MB; a 5 min 4K MP4 is ~1.5 GB). Bump higher in configuration
    ///     when accepting long-form 4K, drop lower for image-only deployments.
    /// </summary>
    public long MaxUploadBytes { get; init; } = 2L * 1024 * 1024 * 1024;

    /// <summary>
    ///     Maximum number of video rungs the pipeline encodes in parallel for a single asset. Defaults
    ///     to <b>1</b> (strict sequential, smallest-first — the original safe behaviour). Bump to 2+
    ///     only when the worker host has spare CPU/RAM: each concurrent ffmpeg process can pin a core
    ///     and allocate hundreds of MB for high-resolution input frames.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Tradeoff: parallelism shortens wall-clock for the full ladder but breaks the
    ///         smallest-first durability guarantee — the order in which variants finish (and therefore
    ///         commit to the DB) is no longer strictly ascending. The smallest rung still <i>starts</i>
    ///         first, so it usually still <i>finishes</i> first, but UIs that depend on observing the low
    ///         rung before the high one should leave this at 1.
    ///     </para>
    ///     <para>
    ///         Image processing is always sequential — image breakpoints finish in milliseconds and
    ///         parallelising them would consume more memory than it saves time.
    ///     </para>
    /// </remarks>
    public int MaxConcurrentVideoRungs { get; init; } = 1;

    /// <summary>
    ///     Additional video codecs to emit per rung, on top of the always-on H.264. Empty by default
    ///     because HEVC/AV1 encoders are 5–20× slower than H.264 and most operators don't need them
    ///     until bandwidth costs become significant.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Each additional codec multiplies encoding time linearly — a 1080p source with the
    ///         full ladder (480/720/1080) plus HEVC produces 6 variants; plus AV1 produces 9. Combine
    ///         with <see cref="MaxConcurrentVideoRungs" /> > 1 on hosts with spare CPU to keep wall-clock
    ///         in check.
    ///     </para>
    ///     <para>
    ///         FFmpeg must be built with <c>libx265</c> (HEVC) and/or <c>libsvtav1</c> (AV1). The
    ///         stock Ubuntu <c>ffmpeg</c> package ships both; minimal Alpine builds may not.
    ///     </para>
    /// </remarks>
    public List<VideoCodecKind> AdditionalVideoCodecs { get; init; } = new();

    /// <summary>
    ///     When true, the pipeline also emits an HLS (HTTP Live Streaming) ladder. Required for
    ///     long-form video, mobile-first apps, and any product where users may scrub or pause heavy
    ///     content. Off by default — progressive MP4 is sufficient for short social clips and the
    ///     HLS step roughly doubles wall-clock for the video portion of the pipeline.
    /// </summary>
    public bool EnableHls { get; init; }

    /// <summary>
    ///     HLS segment duration in seconds. Apple recommends 6 s for VOD; lower values reduce
    ///     rebuffer time but multiply the segment count and storage requests.
    /// </summary>
    public int HlsSegmentDurationSeconds { get; init; } = 6;
}