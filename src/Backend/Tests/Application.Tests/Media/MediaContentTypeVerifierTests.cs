using Application.Modules.Media.Common;

namespace Application.Tests.Media;

/// <summary>
///     Unit tests for the magic-byte sniffer that gates upload completion. Verifies the supported
///     formats are detected, that mismatches are caught, and that the MP4/QuickTime equivalence
///     (both share an ISO-BMFF container) is honoured.
/// </summary>
public sealed class MediaContentTypeVerifierTests
{
    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D }, "image/png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x10, 0x00 }, "image/gif")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x10, 0x00 }, "image/gif")]
    [InlineData(new byte[]
    {
        0x52, 0x49, 0x46, 0x46, // "RIFF"
        0x24, 0x00, 0x00, 0x00, // size
        0x57, 0x45, 0x42, 0x50 // "WEBP"
    }, "image/webp")]
    [InlineData(new byte[]
    {
        0x00, 0x00, 0x00, 0x20, // box size
        0x66, 0x74, 0x79, 0x70, // "ftyp"
        0x69, 0x73, 0x6F, 0x6D // brand "isom"
    }, "video/mp4")]
    [InlineData(new byte[]
    {
        0x00, 0x00, 0x00, 0x20,
        0x66, 0x74, 0x79, 0x70,
        0x71, 0x74, 0x20, 0x20 // brand "qt  "
    }, "video/quicktime")]
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81 }, "video/webm")]
    public void Detect_recognises_known_signatures(byte[] header, string expected)
    {
        var detected = MediaContentTypeVerifier.Detect(header);
        Assert.Equal(expected, detected);
    }

    [Theory]
    [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })] // all zeros
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 })] // PE/EXE header
    [InlineData(new byte[] { 0x3C, 0x21, 0x44, 0x4F, 0x43, 0x54, 0x59, 0x50 })] // "<!DOCTYP"
    public void Detect_returns_null_for_unsupported_payloads(byte[] header)
    {
        Assert.Null(MediaContentTypeVerifier.Detect(header));
    }

    [Theory]
    [InlineData("image/jpeg", "image/jpeg", true)]
    [InlineData("image/jpg", "image/jpeg", true)]
    [InlineData("video/mp4", "video/quicktime", true)]
    [InlineData("video/quicktime", "video/mp4", true)]
    [InlineData("image/png", "image/jpeg", false)]
    [InlineData("image/webp", "image/png", false)]
    [InlineData("video/webm", "video/mp4", false)]
    public void IsCompatible_treats_mp4_family_as_interchangeable(string declared, string detected, bool expected)
    {
        Assert.Equal(expected, MediaContentTypeVerifier.IsCompatible(declared, detected));
    }
}