using Application.Contracts.Media;

namespace Infrastructure.Media;

/// <summary>
///     Registered when <see cref="VideoProcessingOptions.FfmpegPath" /> is not configured. Throws on
///     every call so the operator gets a loud signal that video processing is disabled, instead of
///     silently producing empty variants.
/// </summary>
public sealed class NullVideoProcessingService : IVideoProcessingService
{
    private const string Message =
        "Video processing is disabled. Configure VideoProcessing:FfmpegPath and FfprobePath, or move processing to a dedicated worker.";

    public Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(Message);
    }

    public Task<ProcessedVideoFrame> ExtractPosterAsync(Stream source, TimeSpan captureOffset,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(Message);
    }

    public Task<ProcessedVideo> TranscodeToMp4Async(
        Stream source,
        int maxHeightPx,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(Message);
    }

    public Task<HlsTranscodeSession> TranscodeToHlsAsync(
        Stream source,
        int maxHeightPx,
        int segmentSeconds,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(Message);
    }
}