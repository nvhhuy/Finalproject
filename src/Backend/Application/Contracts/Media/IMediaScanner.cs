namespace Application.Contracts.Media;

/// <summary>
/// Pluggable malware / NSFW scanner that the media pipeline consults <i>before</i> any expensive
/// processing happens. A scanner that returns <see cref="ScanVerdict.Quarantine"/> stops the
/// pipeline and parks the asset in <c>Failed</c> — variants are never created, bytes are never
/// served back to other users.
/// </summary>
/// <remarks>
/// <para>The abstraction is intentionally minimal so multiple providers can slot in: ClamAV via
/// clamd for traditional AV; AWS Rekognition / Azure Content Moderator / Google Cloud Vision for
/// content moderation; in-house ML services for product-specific policy. Compose multiple
/// scanners by registering several implementations and wrapping them with a composite that
/// returns the strongest verdict.</para>
///
/// <para>Default registration is <c>NullMediaScanner</c> which always returns <see cref="ScanVerdict.Clean"/>
/// — explicit opt-in keeps test environments unscanned without surprise.</para>
/// </remarks>
public interface IMediaScanner
{
    /// <summary>
    /// Scans <paramref name="payload"/> and returns a verdict. The implementation reads the stream
    /// to completion; callers must supply a stream they don't need to read again afterwards.
    /// </summary>
    Task<MediaScanResult> ScanAsync(Stream payload, string contentType, CancellationToken cancellationToken = default);
}

/// <summary>Verdict returned by an <see cref="IMediaScanner"/>.</summary>
public enum ScanVerdict
{
    /// <summary>Bytes are safe to process.</summary>
    Clean = 1,

    /// <summary>Scanner couldn't reach a conclusion (timeout, transient backend error). Caller decides whether to fail-open or fail-closed.</summary>
    Unknown = 2,

    /// <summary>Bytes are confirmed unsafe — block the pipeline immediately.</summary>
    Quarantine = 3,
}

/// <summary>
/// Detailed scan outcome. The <see cref="Detail"/> is recorded in the asset's <c>ErrorMessage</c>
/// when a quarantine triggers, so operators have a starting point for incident review.
/// </summary>
public sealed record MediaScanResult(ScanVerdict Verdict, string Detail);