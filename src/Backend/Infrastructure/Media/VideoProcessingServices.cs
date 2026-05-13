using Application.Contracts.Media;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media;

public sealed class NullVideoProcessingService : IVideoProcessingService
{
    public Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default) =>
        Task.FromResult(new VideoProbeResult(null, null, null, null, null));

    public Task ExtractThumbnailAsync(Stream videoSource, Stream destinationJpeg, TimeSpan captureOffset, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("VideoProcessing: configure FfmpegPath and FfprobePath, or replace IVideoProcessingService with your worker implementation.");

    public Task TranscodeToMp4Async(Stream source, Stream destination, VideoTranscodeOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Video transcoding is not enabled. Configure FFmpeg paths or plug a media worker.");
}

public sealed class VideoProcessingOptions
{
    public const string SectionName = "VideoProcessing";
    public string? FfmpegPath { get; init; }
    public string? FfprobePath { get; init; }
}

/// <summary>Local FFmpeg/ffprobe CLI integration (suitable for workers or dedicated media nodes).</summary>
public sealed class FfmpegCliVideoProcessingService : IVideoProcessingService
{
    private readonly VideoProcessingOptions _options;

    public FfmpegCliVideoProcessingService(IOptions<VideoProcessingOptions> options)
    {
        _options = options.Value;
    }

    public async Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FfprobePath))
            return new VideoProbeResult(null, null, null, null, null);

        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _options.FfprobePath,
                Arguments = "-v quiet -print_format json -show_format -show_streams -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.Start();
        await source.CopyToAsync(proc.StandardInput.BaseStream, cancellationToken);
        proc.StandardInput.Close();
        string json = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);
        if (proc.ExitCode != 0)
            return new VideoProbeResult(null, null, null, null, null);

        // Lightweight parse: avoid hard dependency on System.Text.Json nodes shape across ffprobe versions.
        TimeSpan? duration = null;
        if (json.Contains("\"duration\"", StringComparison.Ordinal))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("format", out var format) &&
                    format.TryGetProperty("duration", out var dur) &&
                    dur.TryGetDouble(out double seconds))
                    duration = TimeSpan.FromSeconds(seconds);
            }
            catch
            {
                // ignore parse errors
            }
        }

        return new VideoProbeResult(duration, null, null, null, null);
    }

    public async Task ExtractThumbnailAsync(Stream videoSource, Stream destinationJpeg, TimeSpan captureOffset, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FfmpegPath))
            throw new InvalidOperationException("VideoProcessing:FfmpegPath is not configured.");

        string args = $"-hide_banner -loglevel error -ss {captureOffset.TotalSeconds:0.###} -i pipe:0 -frames:v 1 -f image2 pipe:1";
        using var proc = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _options.FfmpegPath,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.Start();
        _ = Task.Run(async () =>
        {
            await videoSource.CopyToAsync(proc.StandardInput.BaseStream, cancellationToken);
            proc.StandardInput.Close();
        }, cancellationToken);

        await proc.StandardOutput.BaseStream.CopyToAsync(destinationJpeg, cancellationToken);
        await proc.WaitForExitAsync(cancellationToken);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException("ffmpeg thumbnail extraction failed.");
    }

    public Task TranscodeToMp4Async(Stream source, Stream destination, VideoTranscodeOptions options, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Server-side transcode pipeline is intentionally not implemented in the API template — offload to a worker/queue.");
}
