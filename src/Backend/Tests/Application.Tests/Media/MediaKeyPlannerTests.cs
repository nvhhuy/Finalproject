using Application.Contracts.Media;
using Application.Modules.Media.Common;
using Domains.MediaAggregate;

namespace Application.Tests.Media;

/// <summary>
/// Unit tests for <see cref="MediaKeyPlanner"/>. The planner is the single source of truth for
/// bucket layout — changes here cascade to every existing storage object, so any modification
/// must be deliberate. These tests pin the current scheme so accidental drift breaks loudly.
/// </summary>
public sealed class MediaKeyPlannerTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Asset = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void OriginalKey_normalises_extension_to_lowercase_with_leading_dot()
    {
        Assert.Equal(
            "media/11111111111111111111111111111111/22222222222222222222222222222222/original.jpg",
            MediaKeyPlanner.OriginalKey(Owner, Asset, "JPG"));
        Assert.Equal(
            "media/11111111111111111111111111111111/22222222222222222222222222222222/original.png",
            MediaKeyPlanner.OriginalKey(Owner, Asset, ".PNG"));
        Assert.Equal(
            "media/11111111111111111111111111111111/22222222222222222222222222222222/original",
            MediaKeyPlanner.OriginalKey(Owner, Asset, null));
    }

    [Fact]
    public void VideoVariantKey_uses_naked_mp4_for_default_h264_codec()
    {
        // Backward compat: existing 1.0 clients only know "/video-{h}p.mp4" — keep that shape for
        // H.264 so storage layouts don't churn when nothing about playback changed.
        Assert.Equal(
            "media/11111111111111111111111111111111/22222222222222222222222222222222/video-720p.mp4",
            MediaKeyPlanner.VideoVariantKey(Owner, Asset, 720));
    }

    [Theory]
    [InlineData(VideoCodecKind.H265, "video-1080p.hevc.mp4")]
    [InlineData(VideoCodecKind.Av1, "video-1080p.av1.mp4")]
    public void VideoVariantKey_adds_codec_suffix_for_non_h264_codecs(VideoCodecKind codec, string expectedTail)
    {
        // Codec in the filename means orphan cleanup + projection don't need to consult a DB
        // column to classify variants — the URL is self-describing.
        string key = MediaKeyPlanner.VideoVariantKey(Owner, Asset, 1080, codec);
        Assert.EndsWith(expectedTail, key);
    }

    [Fact]
    public void Hls_keys_share_the_asset_prefix_so_listing_one_asset_returns_every_artefact()
    {
        // Critical for the orphan cleanup + delete-asset workflows: every artefact of one asset
        // lives under media/{owner}/{asset}/, and the HLS subtree is no exception.
        string prefix = MediaKeyPlanner.AssetPrefix(Owner, Asset);
        Assert.StartsWith(prefix, MediaKeyPlanner.HlsMasterKey(Owner, Asset));
        Assert.StartsWith(prefix, MediaKeyPlanner.HlsMediaPlaylistKey(Owner, Asset, 720));
        Assert.StartsWith(prefix, MediaKeyPlanner.HlsSegmentPrefix(Owner, Asset, 720));
    }

    [Fact]
    public void HlsMediaPlaylistKey_is_relative_to_master_so_player_resolution_works()
    {
        // The master.m3u8 references rungs as "{height}p/index.m3u8" — verify the keys we PUT
        // match what the player will GET via relative resolution from master.m3u8's URL.
        string master = MediaKeyPlanner.HlsMasterKey(Owner, Asset);
        string playlist = MediaKeyPlanner.HlsMediaPlaylistKey(Owner, Asset, 720);

        // Relative path from master.m3u8 to playlist must be "720p/index.m3u8".
        string masterDir = master[..(master.LastIndexOf('/') + 1)];
        string relative = playlist[masterDir.Length..];
        Assert.Equal("720p/index.m3u8", relative);
    }

    [Theory]
    [InlineData("image/jpeg", MediaKind.Image)]
    [InlineData("image/png", MediaKind.Image)]
    [InlineData("video/mp4", MediaKind.Video)]
    [InlineData("video/webm", MediaKind.Video)]
    public void KindFromContentType_recognises_supported_media_types(string contentType, MediaKind expected) =>
        Assert.Equal(expected, MediaKeyPlanner.KindFromContentType(contentType));

    [Fact]
    public void KindFromContentType_rejects_unsupported_types() =>
        Assert.Throws<ArgumentException>(() => MediaKeyPlanner.KindFromContentType("application/zip"));
}
