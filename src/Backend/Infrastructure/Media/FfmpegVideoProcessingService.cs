using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Contracts.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media;

/// <summary>
///     FFmpeg/ffprobe CLI integration. Designed for the media worker host where FFmpeg is installed
///     either in the container image or on the node. The API host can also use this if the operator
///     configures the paths, but production deployments typically isolate FFmpeg in the worker.
/// </summary>
/// <remarks>
///     Every operation spills the input stream to a temporary file before invoking the CLI because
///     FFmpeg's STDIN piping breaks for non-seekable formats; outputs are streamed back to a
///     <see cref="MemoryStream" /> so we don't have to manage temp-file lifetime past the call.
/// </remarks>
public sealed class FfmpegVideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<FfmpegVideoProcessingService> _logger;
    private readonly VideoProcessingOptions _options;

    public FfmpegVideoProcessingService(IOptions<VideoProcessingOptions> options,
        ILogger<FfmpegVideoProcessingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<VideoProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        EnsureConfigured(false);
        var inputPath = await SpillToTempAsync(source, ".bin", cancellationToken);
        try
        {
            var args = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"";
            var (exitCode, stdout, stderr) = await RunAsync(_options.FfprobePath!, args, cancellationToken);
            if (exitCode != 0)
            {
                _logger.LogWarning("ffprobe failed ({Exit}): {Stderr}", exitCode, stderr);
                return new VideoProbeResult(null, null, null, null, null);
            }

            return ParseProbe(stdout);
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    public async Task<ProcessedVideoFrame> ExtractPosterAsync(Stream source, TimeSpan captureOffset,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(true);
        var inputPath = await SpillToTempAsync(source, ".bin", cancellationToken);
        var outputPath = Path.Combine(Path.GetTempPath(), $"poster-{Guid.NewGuid():N}.jpg");

        try
        {
            var args =
                $"-hide_banner -loglevel error -ss {captureOffset.TotalSeconds:0.###} -i \"{inputPath}\" -frames:v 1 -q:v 4 \"{outputPath}\"";

            var (exitCode, _, stderr) = await RunAsync(_options.FfmpegPath!, args, cancellationToken);
            if (exitCode != 0 || !File.Exists(outputPath))
                throw new InvalidOperationException($"ffmpeg poster extraction failed: {stderr}");

            // Return a streaming wrapper that deletes the temp file on dispose. Posters are
            // small (<100 KB usually) so MemoryStream would be fine, but we use the same shape
            // as the transcode path for consistency and to avoid one more memory copy.
            var byteSize = new FileInfo(outputPath).Length;
            var stream = TempFileStream.OpenRead(outputPath);
            // Width/height aren't returned by the CLI command above; downstream can probe the JPEG
            // separately with the image service if those values are required.
            return new ProcessedVideoFrame(stream, "image/jpeg", 0, 0, byteSize);
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }
        finally
        {
            TryDelete(inputPath);
            // outputPath is now owned by the returned TempFileStream on success — only the
            // failure path above explicitly deletes it.
        }
    }

    public async Task<ProcessedVideo> TranscodeToMp4Async(
        Stream source,
        int maxHeightPx,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured(true);
        var inputPath = await SpillToTempAsync(source, ".bin", cancellationToken);
        var outputPath = Path.Combine(Path.GetTempPath(), $"transcode-{Guid.NewGuid():N}.mp4");

        try
        {
            var args = BuildTranscodeArgs(inputPath, outputPath, maxHeightPx, codec);

            var (exitCode, _, stderr) = await RunAsync(_options.FfmpegPath!, args, cancellationToken);
            if (exitCode != 0 || !File.Exists(outputPath))
                throw new InvalidOperationException($"ffmpeg transcode failed (codec={codec}): {stderr}");

            // Streamed return: don't read the whole MP4 into managed memory. At four rungs
            // running concurrently a single 4K source can easily produce >1 GB of variant data;
            // buffering each rung's bytes would OOM a 2 GB worker. The probe below opens the
            // same file in a separate FileStream so we don't disturb the position of the stream
            // we'll hand back to the caller.
            var byteSize = new FileInfo(outputPath).Length;
            VideoProbeResult probed;
            await using (var probeInput = File.OpenRead(outputPath))
            {
                probed = await ProbeAsync(probeInput, cancellationToken);
            }

            var stream = TempFileStream.OpenRead(outputPath);
            return new ProcessedVideo(
                stream,
                MimeTypeForCodec(codec),
                probed.Width ?? 0,
                probed.Height ?? 0,
                probed.Duration ?? TimeSpan.Zero,
                byteSize);
        }
        catch
        {
            TryDelete(outputPath);
            throw;
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    public async Task<HlsTranscodeSession> TranscodeToHlsAsync(
        Stream source,
        int maxHeightPx,
        int segmentSeconds,
        VideoCodecKind codec = VideoCodecKind.H264,
        CancellationToken cancellationToken = default)
    {
        // We intentionally pin to H.264 for the segments regardless of `codec`. Cross-player
        // compatibility for HEVC-in-HLS is limited (Safari only, and requires fMP4 segments,
        // not TS); AV1-in-HLS isn't shipped anywhere. The `codec` parameter is reserved for a
        // future fMP4 variant. Operators who want HEVC/AV1 today should rely on the progressive
        // MP4 variants emitted alongside HLS.
        _ = codec;
        EnsureConfigured(true);
        var inputPath = await SpillToTempAsync(source, ".bin", cancellationToken);

        // Each HLS rung gets its own temp directory; FFmpeg writes index.m3u8 + seg-XXX.ts there.
        // We give the caller a session that hands out file streams from this dir and cleans up on
        // dispose. Don't put it under input's dir — we want lifetimes decoupled.
        var outputDir = Path.Combine(Path.GetTempPath(), $"hls-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var playlistPath = Path.Combine(outputDir, "index.m3u8");
            var segmentPattern = Path.Combine(outputDir, "seg-%03d.ts");

            // -hls_playlist_type vod → emits the EXT-X-PLAYLIST-TYPE:VOD tag, marking the playlist
            // as a complete VOD asset (vs live). VOD enables player optimisations like seeking.
            // -hls_segment_filename has %03d so segments are predictably named for sorting/uploading.
            // -hls_list_size 0 → keep every segment in the playlist (default keeps only last N).
            // HLS pinned to H.264 (see contract); use whatever H.264 encoder the operator
            // configured (libx264 default, or h264_nvenc/h264_videotoolbox for hw accel).
            var hlsEncoder = _options.VideoCodec;
            var argsBuilder = new StringBuilder();
            argsBuilder.Append("-hide_banner -loglevel error ");
            if (!string.IsNullOrWhiteSpace(_options.HwAccel))
                argsBuilder.Append($"-hwaccel {_options.HwAccel} ");
            argsBuilder.Append($"-i \"{inputPath}\" ");
            argsBuilder.Append($"-vf \"scale=-2:'min({maxHeightPx},ih)'\" ");
            argsBuilder.Append($"-c:v {hlsEncoder} ");
            if (IsSoftwareCodec(hlsEncoder))
                argsBuilder.Append($"-preset {_options.Preset} -crf {_options.Crf} ");
            argsBuilder.Append($"-c:a {_options.AudioCodec} -b:a {_options.AudioBitrate} ");
            argsBuilder.Append($"-hls_time {segmentSeconds} -hls_playlist_type vod -hls_list_size 0 ");
            argsBuilder.Append($"-hls_segment_filename \"{segmentPattern}\" ");
            argsBuilder.Append($"\"{playlistPath}\"");

            var (exitCode, _, stderr) = await RunAsync(_options.FfmpegPath!, argsBuilder.ToString(), cancellationToken);
            if (exitCode != 0 || !File.Exists(playlistPath))
                throw new InvalidOperationException($"ffmpeg HLS transcode failed: {stderr}");

            // Probe the playlist + first segment to surface dimensions and duration. We probe
            // the segment because the playlist itself is a text file ffprobe doesn't decode for
            // dimension info, but the first .ts segment does have it.
            var segments = Directory.EnumerateFiles(outputDir, "seg-*.ts")
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            if (segments.Length == 0)
                throw new InvalidOperationException("ffmpeg HLS transcode produced no segments.");

            VideoProbeResult probed;
            await using (var firstSeg = File.OpenRead(segments[0]))
            {
                probed = await ProbeAsync(firstSeg, cancellationToken);
            }

            // Aggregate size + relative names for upload.
            var relativePaths = new List<string> { "index.m3u8" };
            var totalBytes = new FileInfo(playlistPath).Length;
            foreach (var segPath in segments)
            {
                relativePaths.Add(Path.GetFileName(segPath));
                totalBytes += new FileInfo(segPath).Length;
            }

            // Sum duration from the playlist's #EXTINF tags (more accurate than ffprobe on a single segment).
            var totalDuration = ParseHlsPlaylistDuration(playlistPath);

            // Hand off ownership of outputDir to the session — finally-cleanup below intentionally
            // skips it. The session disposes the directory when the caller finishes uploading.
            return new HlsTranscodeSession(
                outputDir,
                relativePaths,
                "index.m3u8",
                probed.Width ?? 0,
                probed.Height ?? maxHeightPx,
                totalDuration,
                totalBytes);
        }
        catch
        {
            // Only clean up on failure — success-path cleanup happens via the session.
            TryDeleteDirectory(outputDir);
            throw;
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    private static TimeSpan ParseHlsPlaylistDuration(string playlistPath)
    {
        double total = 0;
        foreach (var line in File.ReadLines(playlistPath))
        {
            // Each segment line is preceded by "#EXTINF:<duration>,<title>"
            if (!line.StartsWith("#EXTINF:", StringComparison.Ordinal)) continue;
            var payload = line.Substring("#EXTINF:".Length);
            var comma = payload.IndexOf(',');
            if (comma > 0) payload = payload[..comma];
            if (double.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                total += seconds;
        }

        return TimeSpan.FromSeconds(total);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch
        {
            /* best-effort */
        }
    }

    /// <summary>
    ///     Returns the W3C-style content type with codecs parameter. Browsers use this in
    ///     <c>&lt;source type="..."&gt;</c> to skip variants their decoder can't play, so the suffix
    ///     is critical for adaptive playback on mixed-device audiences.
    /// </summary>
    /// <remarks>
    ///     The codecs string follows RFC 6381. We emit one representative profile per codec — a more
    ///     accurate value would re-probe each output for its actual <c>codec_string</c>, but the
    ///     representative values match what 99% of real-world clips encode to under default settings.
    /// </remarks>
    private static string MimeTypeForCodec(VideoCodecKind codec)
    {
        return codec switch
        {
            VideoCodecKind.H264 => "video/mp4",
            VideoCodecKind.H265 => "video/mp4; codecs=hev1.1.6.L93.B0",
            VideoCodecKind.Av1 => "video/mp4; codecs=av01.0.05M.08",
            _ => "video/mp4"
        };
    }

    private void EnsureConfigured(bool ffmpeg)
    {
        var path = ffmpeg ? _options.FfmpegPath : _options.FfprobePath;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                ffmpeg
                    ? "VideoProcessing:FfmpegPath is not configured."
                    : "VideoProcessing:FfprobePath is not configured.");
    }

    /// <summary>
    ///     Builds the FFmpeg argument string for one transcode rung. Honours the configured codec,
    ///     preset, audio settings and optional hardware decode acceleration so operators can switch
    ///     to GPU encoding (e.g. <c>h264_nvenc</c>) without redeploying the worker image.
    /// </summary>
    /// <remarks>
    ///     Layout (in order): hwaccel → input → scale filter → video codec → encoder tuning → audio codec → faststart →
    ///     output.
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <c>-vf scale=-2:min(h,ih)</c> — keeps aspect ratio, bounds height, ensures even width (libx264
    ///                 + GPU encoders require even dimensions).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>-movflags +faststart</c> — moves the MP4 moov atom to the front so the browser can begin
    ///                 playback before the container finishes loading.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description><c>-preset</c> is only emitted for libx264/libx265; HW encoders use their own tuning knobs.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    private string BuildTranscodeArgs(string inputPath, string outputPath, int maxHeightPx, VideoCodecKind codec)
    {
        // Pick the actual FFmpeg encoder name for this codec. For H.264 we honour the operator's
        // configured encoder (libx264 / h264_nvenc / h264_videotoolbox) so GPU acceleration works
        // for the primary rung. For HEVC/AV1 we use the standard software encoders — operators
        // who want HW-accelerated HEVC/AV1 can override via configuration.
        var ffmpegCodec = codec switch
        {
            VideoCodecKind.H264 => _options.VideoCodec,
            VideoCodecKind.H265 => "libx265",
            VideoCodecKind.Av1 => "libsvtav1",
            _ => throw new ArgumentOutOfRangeException(nameof(codec), $"Unsupported codec: {codec}")
        };

        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error ");

        if (!string.IsNullOrWhiteSpace(_options.HwAccel))
            args.Append($"-hwaccel {_options.HwAccel} ");

        args.Append($"-i \"{inputPath}\" ");
        args.Append($"-vf \"scale=-2:'min({maxHeightPx},ih)'\" ");
        args.Append($"-c:v {ffmpegCodec} ");

        // Software codecs accept -preset / -crf; HW encoders silently ignore them but FFmpeg warns.
        // Only emit those flags when we know the encoder is software to keep logs clean.
        if (IsSoftwareCodec(ffmpegCodec))
        {
            // libsvtav1 uses 0–13 preset (lower=slower=smaller); we'd need a separate mapping for
            // perfect tuning, but the default options.Preset is a libx264 name — for AV1 we skip
            // the preset flag rather than passing an invalid name. CRF works on all three.
            if (!ffmpegCodec.Equals("libsvtav1", StringComparison.OrdinalIgnoreCase))
                args.Append($"-preset {_options.Preset} ");
            args.Append($"-crf {_options.Crf} ");
        }

        // HEVC in MP4 requires the hvc1 tag for Safari to play it. Without this, Safari fails
        // silently with a black video — a footgun worth defending against by default.
        if (codec == VideoCodecKind.H265)
            args.Append("-tag:v hvc1 ");

        args.Append($"-c:a {_options.AudioCodec} -b:a {_options.AudioBitrate} ");
        args.Append($"-movflags +faststart \"{outputPath}\"");
        return args.ToString();
    }

    private static bool IsSoftwareCodec(string codec)
    {
        return codec.Equals("libx264", StringComparison.OrdinalIgnoreCase)
               || codec.Equals("libx265", StringComparison.OrdinalIgnoreCase)
               || codec.Equals("libaom-av1", StringComparison.OrdinalIgnoreCase)
               || codec.Equals("libsvtav1", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> SpillToTempAsync(Stream source, string extension,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"media-{Guid.NewGuid():N}{extension}");
        await using var fs = File.Create(path);
        await source.CopyToAsync(fs, cancellationToken);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            /* best effort */
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string fileName, string args,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.Start();
        var stdOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await stdOut, await stdErr);
    }

    private static VideoProbeResult ParseProbe(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            TimeSpan? duration = null;
            int? width = null;
            int? height = null;
            string? videoCodec = null;
            string? audioCodec = null;

            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var dur) &&
                double.TryParse(dur.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                duration = TimeSpan.FromSeconds(seconds);

            if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
                foreach (var stream in streams.EnumerateArray())
                {
                    var type = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;
                    if (type == "video" && videoCodec is null)
                    {
                        videoCodec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;
                        if (stream.TryGetProperty("width", out var w)) width = w.GetInt32();
                        if (stream.TryGetProperty("height", out var h)) height = h.GetInt32();
                    }
                    else if (type == "audio" && audioCodec is null)
                    {
                        audioCodec = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : null;
                    }
                }

            return new VideoProbeResult(duration, width, height, videoCodec, audioCodec);
        }
        catch (JsonException)
        {
            return new VideoProbeResult(null, null, null, null, null);
        }
    }
}