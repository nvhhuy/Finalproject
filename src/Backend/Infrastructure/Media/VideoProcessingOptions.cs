namespace Infrastructure.Media;

/// <summary>
///     FFmpeg / ffprobe binary locations + transcoding profile. Defaults target software H.264
///     encoding via <c>libx264</c> which works on any host. For 4K throughput in production, switch
///     to a hardware-accelerated codec (5–10× faster) — see <see cref="VideoCodec" />.
/// </summary>
public sealed class VideoProcessingOptions
{
    public const string SectionName = "VideoProcessing";

    /// <summary>
    ///     Absolute path to <c>ffmpeg</c>. Leave empty to fall back to
    ///     <see cref="Infrastructure.Media.NullVideoProcessingService" />.
    /// </summary>
    public string? FfmpegPath { get; init; }

    /// <summary>Absolute path to <c>ffprobe</c>. Required for duration/dimension extraction.</summary>
    public string? FfprobePath { get; init; }

    /// <summary>Cap transcoded video height in pixels. Width scales with aspect ratio.</summary>
    public int MaxTranscodeHeightPx { get; init; } = 720;

    /// <summary>
    ///     FFmpeg <c>-c:v</c> value. Common options:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>libx264</c> (default) — software H.264, universal compatibility, slowest.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>h264_videotoolbox</c> — Apple Silicon / Intel Mac HW encoder. 5–10× faster than libx264,
    ///                 supported on macOS workers.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>h264_nvenc</c> — NVIDIA GPU encoder. 10–20× faster, requires a CUDA-capable card and the
    ///                 right FFmpeg build.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description><c>h264_qsv</c> — Intel Quick Sync. Available on most Intel CPUs with iGPU.</description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>libx265</c> / <c>hevc_*</c> — HEVC (H.265). ~50% smaller files but limited Safari pre-13
    ///                 support and patent licensing concerns.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </summary>
    public string VideoCodec { get; init; } = "libx264";

    /// <summary>
    ///     FFmpeg <c>-c:a</c> value. <c>aac</c> is broadly supported; <c>libopus</c> is smaller but not iOS-friendly
    ///     inside MP4.
    /// </summary>
    public string AudioCodec { get; init; } = "aac";

    /// <summary>Audio target bitrate. 128 kbps is the streaming-podcast standard.</summary>
    public string AudioBitrate { get; init; } = "128k";

    /// <summary>
    ///     libx264 <c>-preset</c>. Software-only setting (HW codecs ignore it). Trade-offs:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>ultrafast</c> — biggest file, fastest encode.</description>
    ///         </item>
    ///         <item>
    ///             <description><c>veryfast</c> (default) — good throughput for worker load.</description>
    ///         </item>
    ///         <item>
    ///             <description><c>medium</c> — balanced, smaller files, slower.</description>
    ///         </item>
    ///         <item>
    ///             <description><c>slow</c> / <c>slower</c> — smallest files, suitable for offline VOD ingestion only.</description>
    ///         </item>
    ///     </list>
    /// </summary>
    public string Preset { get; init; } = "veryfast";

    /// <summary>
    ///     Constant Rate Factor passed to libx264 (0–51, lower = better quality + bigger file). 23 = visually transparent
    ///     for most content.
    /// </summary>
    public int Crf { get; init; } = 23;

    /// <summary>
    ///     Optional <c>-hwaccel</c> value for input decoding (separate from <see cref="VideoCodec" />
    ///     which controls output encoding). Examples: <c>videotoolbox</c>, <c>cuda</c>, <c>qsv</c>,
    ///     <c>vaapi</c>. Leave empty for software decoding.
    /// </summary>
    public string? HwAccel { get; init; }
}