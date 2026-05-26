using Application.Contracts.Media;
using Infrastructure.Media;

namespace Infrastructure.Tests.Media;

/// <summary>
/// Unit tests for <see cref="ClamdResponseParser"/>. The parser is the only piece of the scanner
/// pipeline that can be exercised without a real clamd daemon; we cover every documented
/// response shape so regressions surface immediately.
/// </summary>
public sealed class ClamdResponseParserTests
{
    [Theory]
    [InlineData("stream: OK")]
    [InlineData("stream: OK\n")]
    [InlineData("  stream: OK  ")]
    public void Parse_returns_clean_for_OK_response(string response)
    {
        MediaScanResult result = ClamdResponseParser.Parse(response);
        Assert.Equal(ScanVerdict.Clean, result.Verdict);
    }

    [Theory]
    [InlineData("stream: Win.Test.EICAR_HDB-1 FOUND", "Win.Test.EICAR_HDB-1")]
    [InlineData("stream: Eicar-Test-Signature FOUND", "Eicar-Test-Signature")]
    [InlineData("stream: Trojan.Generic.123 FOUND", "Trojan.Generic.123")]
    public void Parse_returns_quarantine_for_FOUND_response(string response, string expectedSignaturePart)
    {
        MediaScanResult result = ClamdResponseParser.Parse(response);
        Assert.Equal(ScanVerdict.Quarantine, result.Verdict);
        Assert.Contains(expectedSignaturePart, result.Detail);
    }

    [Theory]
    [InlineData("stream: INSTREAM size limit exceeded. ERROR")]
    [InlineData("clamd not ready")]
    [InlineData("garbage payload")]
    public void Parse_returns_unknown_for_unrecognised_responses(string response)
    {
        // ERROR responses + anything we don't explicitly recognise map to Unknown so the
        // caller's fail-open/closed policy decides — we don't presume to fail in either
        // direction at the parse layer.
        MediaScanResult result = ClamdResponseParser.Parse(response);
        Assert.Equal(ScanVerdict.Unknown, result.Verdict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_returns_unknown_for_empty_response(string? response)
    {
        MediaScanResult result = ClamdResponseParser.Parse(response!);
        Assert.Equal(ScanVerdict.Unknown, result.Verdict);
    }
}
