using Application.Contracts.Media;

namespace Infrastructure.Media;

/// <summary>
///     Parser for ClamAV <c>clamd</c> INSTREAM responses. Extracted to a static helper so the
///     protocol decoding is testable without standing up a real clamd daemon.
/// </summary>
/// <remarks>
///     Clamd emits a single response line per scan:
///     <list type="bullet">
///         <item>
///             <description><c>stream: OK</c> — payload is clean.</description>
///         </item>
///         <item>
///             <description><c>stream: &lt;signature&gt; FOUND</c> — payload matched a malware signature.</description>
///         </item>
///         <item>
///             <description><c>stream: &lt;reason&gt; ERROR</c> — clamd couldn't complete the scan (typically size limit).</description>
///         </item>
///     </list>
/// </remarks>
public static class ClamdResponseParser
{
    public static MediaScanResult Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new MediaScanResult(ScanVerdict.Unknown, "empty clamd response");

        var trimmed = response.Trim();

        if (trimmed.EndsWith(" OK", StringComparison.Ordinal))
            return new MediaScanResult(ScanVerdict.Clean, trimmed);

        if (trimmed.EndsWith(" FOUND", StringComparison.Ordinal))
            return new MediaScanResult(ScanVerdict.Quarantine, trimmed);

        // ERROR responses from clamd cover transient and permanent failures (size cap, memory
        // pressure, signature database load). Map to Unknown so the caller can fail-open or
        // fail-closed per policy — we don't have enough signal to decide.
        return new MediaScanResult(ScanVerdict.Unknown, $"unexpected clamd response: {trimmed}");
    }
}