namespace Application.Contracts.Media;

/// <summary>
///     Video processing operations. Implementations call out to an FFmpeg/ffprobe CLI; if those tools
///     are unavailable on the host, register the null implementation so commands fail loud rather than
///     silently degrading.
/// </summary>
public interface IVideoProcessingService
{
    /// <summary>Reads container metadata (duration, dimensions, codec) from the supplied bytes.</summary>
    Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extracts a still frame at <paramref name="captureOffset" /> as a JPEG. Used to populate the
    ///     poster image shown before the user hits play.
    /// </summary>
    Task<ProcessedVideoFrame> ExtractPosterAsync(Stream source, TimeSpan captureOffset,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Transcodes the input to MP4 capped at <paramref name="maxHeightPx" /> using
    ///     <paramref name="codec" /> as the video encoder. AAC is used for audio across all codecs so
    ///     downstream players don't need a separate audio negotiation. Returns a stream the caller
    ///     uploads to the bucket as a video variant; the original is preserved independently.
    /// </summary>
    Task<ProcessedVideo> TranscodeToMp4Async(
        Stream source,
        int maxHeightPx,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Transcodes the input as an HLS rung — a media playlist (<c>index.m3u8</c>) plus a sequence
    ///     of TS segments. Returns a session the caller iterates to upload each artefact, then
    ///     disposes to clean up temporary files. The session is short-lived; it must not outlive the
    ///     HTTP request / worker handler.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Codec note:</b> HEVC and AV1 don't carry inside MPEG-TS segments in mainstream
    ///         players (HEVC works on Safari with fMP4 segments, AV1 doesn't work in HLS at all yet).
    ///         We therefore emit H.264 segments for HLS regardless of <paramref name="codec" />; the
    ///         parameter exists so the contract can be extended later (HEVC-in-fMP4 master playlists)
    ///         without breaking callers.
    ///     </para>
    /// </remarks>
    Task<HlsTranscodeSession> TranscodeToHlsAsync(
        Stream source,
        int maxHeightPx,
        int segmentSeconds,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default);
}

public sealed record VideoProbeResult(
    TimeSpan? Duration,
    int? Width,
    int? Height,
    string? VideoCodec,
    string? AudioCodec);

public sealed record ProcessedVideoFrame(Stream Content, string ContentType, int Width, int Height, long ByteSize);

public sealed record ProcessedVideo(
    Stream Content,
    string ContentType,
    int Width,
    int Height,
    TimeSpan Duration,
    long ByteSize);

/// <summary>
///     Selectable video codec for the transcoded variants. Maps to specific FFmpeg encoders inside
///     the implementation so callers don't depend on FFmpeg-specific naming.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>H264</b> — universal compatibility, default. Uses <c>libx264</c> (or the GPU encoder
///                 configured in <c>VideoProcessingOptions.VideoCodec</c>).
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>H265 (HEVC)</b> — ~50% smaller files than H.264 at same quality. Supported by Safari 11+,
///                 modern Chrome (with hardware decoder), Edge. Encoded via <c>libx265</c>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>AV1</b> — ~30% smaller than HEVC, royalty-free. Chrome 70+, Firefox, Edge, Safari 17+.
///                 Encoded via <c>libsvtav1</c> (10–20× slower than libx264 — enable only when bandwidth costs justify the
///                 encoder time).
///             </description>
///         </item>
///     </list>
/// </remarks>
public enum VideoCodecKind
{
    H264 = 1,
    H265 = 2,
    Av1 = 3
}

/// <summary>
///     Output handle for an HLS transcode. Exposes the playlist + segment filenames and lets the
///     caller open each one as a stream for upload. The session owns a temp directory that must be
///     cleaned up via <see cref="IAsyncDisposable.DisposeAsync" />.
/// </summary>
/// <remarks>
///     Layout returned by FFmpeg's HLS muxer (under the session's temp dir):
///     <list type="bullet">
///         <item>
///             <description><c>index.m3u8</c> — the media playlist for this rung</description>
///         </item>
///         <item>
///             <description><c>seg-000.ts</c>, <c>seg-001.ts</c>, … — MPEG-TS segments</description>
///         </item>
///     </list>
///     The caller uploads them under <c>media/{owner}/{asset}/hls/{height}p/</c> keeping the same
///     relative names, so the playlist's relative segment references resolve correctly.
/// </remarks>
public sealed class HlsTranscodeSession : IAsyncDisposable
{
    private readonly string _tempDirectory;

    public HlsTranscodeSession(
        string tempDirectory,
        IReadOnlyList<string> relativePaths,
        string playlistRelativePath,
        int width,
        int height,
        TimeSpan duration,
        long totalByteSize)
    {
        _tempDirectory = tempDirectory;
        RelativePaths = relativePaths;
        PlaylistRelativePath = playlistRelativePath;
        Width = width;
        Height = height;
        Duration = duration;
        TotalByteSize = totalByteSize;
    }

    public IReadOnlyList<string> RelativePaths { get; }
    public string PlaylistRelativePath { get; }
    public int Width { get; }
    public int Height { get; }
    public TimeSpan Duration { get; }
    public long TotalByteSize { get; }

    public ValueTask DisposeAsync()
    {
        try
        {
            Directory.Delete(_tempDirectory, true);
        }
        catch
        {
            /* best-effort cleanup; OS will eventually reclaim temp space */
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Opens the named artefact for reading. Caller disposes the returned stream.</summary>
    public Stream OpenRead(string relativePath)
    {
        return File.OpenRead(Path.Combine(_tempDirectory, relativePath));
    }
}