namespace Infrastructure.Media;

/// <summary>
/// Configuration for the ClamAV-backed <see cref="ClamdMediaScanner"/>. The scanner talks to
/// <c>clamd</c> over its INSTREAM TCP protocol — operators must run a clamd daemon reachable
/// from the worker (typically a sidecar container or shared service).
/// </summary>
public sealed class MediaScannerOptions
{
    public const string SectionName = "MediaScanner";

    /// <summary>When false, no AV scanning happens regardless of host/port. Default false.</summary>
    public bool Enabled { get; init; }

    /// <summary>Clamd TCP host. Typical local dev: <c>localhost</c>; Docker compose: <c>clamav</c>.</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>Clamd TCP port. Default 3310 per clamd convention.</summary>
    public int Port { get; init; } = 3310;

    /// <summary>How long to wait for clamd to accept the connection.</summary>
    public int ConnectTimeoutSeconds { get; init; } = 5;

    /// <summary>How long to wait for clamd to scan the payload after upload. 4K videos can take 30+ seconds.</summary>
    public int ScanTimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// If true, scanner errors (timeouts, connection refused) return <see cref="Application.Contracts.Media.ScanVerdict.Quarantine"/>
    /// rather than <see cref="Application.Contracts.Media.ScanVerdict.Unknown"/>. Set to true for
    /// fail-closed semantics (e.g. healthcare, finance); leave false for social-media where
    /// transient AV outages shouldn't drop user uploads.
    /// </summary>
    public bool FailClosed { get; init; }

    /// <summary>
    /// Maximum simultaneous TCP connections to clamd. Each in-flight scan holds one connection
    /// from the .NET process's ephemeral-port range; without a cap, a traffic spike can chew
    /// through ports faster than the OS recycles them (TIME_WAIT). Default 16 — enough for
    /// moderate workloads, low enough that the scanner host doesn't drown a single clamd
    /// instance.
    /// </summary>
    public int MaxConcurrentScans { get; init; } = 16;
}
