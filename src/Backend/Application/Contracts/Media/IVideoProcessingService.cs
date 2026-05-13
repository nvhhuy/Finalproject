namespace Application.Contracts.Media;

/// <summary>Video pipeline hooks (thumbnails, probe, transcoding). Implementations typically shell FFmpeg or call a media worker.</summary>
public interface IVideoProcessingService
{
    /// <summary>Best-effort metadata (requires ffprobe or equivalent).</summary>
    Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default);

    /// <summary>Extract a single JPEG frame at the given offset (requires ffmpeg).</summary>
    Task ExtractThumbnailAsync(Stream videoSource, Stream destinationJpeg, TimeSpan captureOffset, CancellationToken cancellationToken = default);

    /// <summary>Transcode to MP4 H.264/AAC (placeholder: real impl delegates to ffmpeg worker).</summary>
    Task TranscodeToMp4Async(Stream source, Stream destination, VideoTranscodeOptions options, CancellationToken cancellationToken = default);
}

public sealed record VideoProbeResult(TimeSpan? Duration, int? Width, int? Height, string? Codec, string? Container);

public sealed record VideoTranscodeOptions(int? MaxWidth = null, int? MaxHeight = null, int? VideoBitrateKbps = null);
