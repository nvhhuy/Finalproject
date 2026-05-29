using System.Text;
using Application.Contracts.Media;
using Application.Contracts.Storage;
using Application.Contracts.Transactions;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;
using Microsoft.Extensions.Logging;

namespace Application.Modules.Media.Pipeline;

/// <summary>
///     Emits the HLS ladder for a single <see cref="MediaAsset" /> in parallel with the existing MP4
///     rungs. Produces one <c>{height}p/index.m3u8</c> + segment set per applicable rung, then
///     assembles a <c>master.m3u8</c> referencing all of them so players can adapt to bandwidth.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why progressive AND HLS:</b> progressive MP4 supports byte-range seeking but the
///         browser downloads the chosen quality monotonically. HLS lets the player switch rungs mid-
///         playback based on observed throughput, which dramatically improves the experience on flaky
///         mobile networks. We emit both so callers can pick: <c>video</c> for cheap UA-only seeking,
///         HLS for adaptive streaming through a media-source extension.
///     </para>
///     <para>
///         <b>Codec pin:</b> all HLS rungs use H.264 + AAC inside MPEG-TS segments. HEVC-in-HLS
///         only works on Safari with fMP4 segments, AV1-in-HLS isn't broadly shipped. Operators who
///         want HEVC/AV1 should rely on the MP4 ladder.
///     </para>
/// </remarks>
internal sealed class HlsLadderProducer
{
    private readonly ILogger _logger;
    private readonly MediaUploadOptions _options;
    private readonly IObjectStorage _storage;
    private readonly IUnitOfWork _uow;
    private readonly IVideoProcessingService _videoProcessing;

    public HlsLadderProducer(
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

    public async Task ProduceAsync(MediaAsset asset, int[] applicableRungs, CancellationToken cancellationToken)
    {
        var manifestEntries = new List<HlsManifestEntry>();
        foreach (var rung in applicableRungs)
        {
            var entry = await EncodeAndUploadRungAsync(asset, rung, cancellationToken);
            if (entry is not null)
                manifestEntries.Add(entry);
        }

        if (manifestEntries.Count == 0)
            return;

        // Master playlist: one EXT-X-STREAM-INF per rung. BANDWIDTH is the only required
        // attribute; RESOLUTION + CODECS are display niceties some players use for picker UI.
        var masterBuilder = new StringBuilder();
        masterBuilder.AppendLine("#EXTM3U");
        masterBuilder.AppendLine("#EXT-X-VERSION:3");
        foreach (var entry in manifestEntries.OrderBy(e => e.Height))
        {
            masterBuilder.AppendLine(
                $"#EXT-X-STREAM-INF:BANDWIDTH={entry.BandwidthBps},RESOLUTION={entry.Width}x{entry.Height},CODECS=\"avc1.4d401e,mp4a.40.2\"");
            masterBuilder.AppendLine($"{entry.Height}p/index.m3u8");
        }

        var masterKey = MediaKeyPlanner.HlsMasterKey(asset.OwnerUserId, asset.Id.Value);
        var masterBytes = Encoding.UTF8.GetBytes(masterBuilder.ToString());
        await using (var ms = new MemoryStream(masterBytes))
        {
            await _storage.PutAsync(masterKey, ms, "application/vnd.apple.mpegurl", cancellationToken);
        }

        asset.AddVariant(
            MediaVariantKind.VideoHlsMaster,
            masterKey,
            "application/vnd.apple.mpegurl",
            masterBytes.LongLength,
            // Highest rung's dimensions as the representative resolution for the master entry.
            manifestEntries.Max(e => e.Width),
            manifestEntries.Max(e => e.Height),
            manifestEntries.Max(e => e.Duration));
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<HlsManifestEntry?> EncodeAndUploadRungAsync(
        MediaAsset asset,
        int rung,
        CancellationToken cancellationToken)
    {
        await using var src = await _storage.GetAsync(asset.OriginalKey, cancellationToken);
        await using var session = await _videoProcessing.TranscodeToHlsAsync(
            src, rung, _options.HlsSegmentDurationSeconds, VideoCodecKind.H264, cancellationToken);

        var prefix = MediaKeyPlanner.HlsSegmentPrefix(asset.OwnerUserId, asset.Id.Value, rung);

        foreach (var relative in session.RelativePaths)
        {
            var contentType = relative.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                ? "application/vnd.apple.mpegurl"
                : "video/mp2t";

            await using var content = session.OpenRead(relative);
            await _storage.PutAsync(prefix + relative, content, contentType, cancellationToken);
        }

        // Bandwidth targets per rung — defaults from Apple's HLS Authoring Specification for VOD.
        // Operators with atypical content (talking heads vs. action) can override in a future
        // configurable bandwidth table; these are sensible starting points.
        var bandwidthBps = rung switch
        {
            <= 480 => 850_000,
            <= 720 => 2_000_000,
            <= 1080 => 4_500_000,
            <= 1440 => 8_000_000,
            _ => 15_000_000
        };

        return new HlsManifestEntry(
            session.Height,
            session.Width,
            bandwidthBps,
            session.Duration);
    }
}

/// <summary>One row in the HLS master playlist (collected per rung, serialised once all rungs upload).</summary>
internal sealed record HlsManifestEntry(int Height, int Width, int BandwidthBps, TimeSpan Duration);