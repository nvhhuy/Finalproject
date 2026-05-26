namespace Domains.MediaAggregate;

/// <summary>
/// Identifies what a generated variant represents. Multiple variants of the same kind can exist
/// when the pipeline targets several sizes (e.g. <see cref="ImageThumbnail"/> at 256 px and 512 px).
/// </summary>
public enum MediaVariantKind
{
    /// <summary>Untouched bytes uploaded by the user. Stored verbatim for re-processing / archive.</summary>
    Original = 1,

    /// <summary>Square or constrained thumbnail used in lists.</summary>
    ImageThumbnail = 10,

    /// <summary>Aspect-preserving resize at one of the canonical breakpoints.</summary>
    ImageResized = 11,

    /// <summary>Single JPEG frame pulled from a video for the poster image.</summary>
    VideoPoster = 20,

    /// <summary>H.264 MP4 transcode suitable for browser playback.</summary>
    VideoMp4 = 21,

    /// <summary>HEVC (H.265) MP4 transcode — ~50% smaller than H.264 for same quality.</summary>
    VideoMp4Hevc = 22,

    /// <summary>AV1 MP4 transcode — ~30% smaller than HEVC, royalty-free.</summary>
    VideoMp4Av1 = 23,

    /// <summary>
    /// HLS master playlist (the single <c>master.m3u8</c> a player loads first). The accompanying
    /// per-rung media playlists and <c>.ts</c> segments live under the same prefix but aren't
    /// individually tracked as variants — listing them all would explode the row count.
    /// </summary>
    VideoHlsMaster = 24,
}
