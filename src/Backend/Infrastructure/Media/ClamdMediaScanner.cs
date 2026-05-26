using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Sockets;
using System.Text;
using Application.Contracts.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Media;

/// <summary>
/// <see cref="IMediaScanner"/> backed by ClamAV's <c>clamd</c> daemon, talking the INSTREAM
/// protocol over TCP. Streams uploads to clamd in 64 KiB chunks and parses the verdict from the
/// single-line response.
/// </summary>
/// <remarks>
/// <para><b>INSTREAM protocol</b> (clamd manual): client connects, sends literal <c>zINSTREAM\0</c>,
/// then a sequence of <c>[uint32-big-endian length] [chunk bytes]</c> blocks, terminated by a
/// zero-length block. clamd replies with a single line like
/// <c>stream: OK</c>, <c>stream: Win.Test.EICAR_HDB-1 FOUND</c>, or <c>stream: ERROR &lt;detail&gt;</c>.</para>
///
/// <para><b>Sizing:</b> clamd has a server-side <c>StreamMaxLength</c> (default 100 MiB). For
/// social-media workloads that send 1-2 GB 4K clips, raise that on the daemon or pre-segment
/// uploads. This scanner does not chunk-split — it streams whatever the caller hands it.</para>
///
/// <para><b>Concurrency:</b> each scan holds one TCP connection for its full duration (clamd's
/// INSTREAM protocol is stateful — you can't multiplex). The <see cref="MediaScannerOptions.MaxConcurrentScans"/>
/// semaphore caps in-flight scans to prevent ephemeral-port exhaustion on the scanner host;
/// requests waiting on the semaphore queue rather than failing.</para>
///
/// <para><b>Observability:</b> emits four OTel metrics on the <c>FinalProject.Application</c>
/// meter: <c>clamd_scans_total</c> (counter), <c>clamd_quarantines_total</c> (counter,
/// signature-labeled), <c>clamd_errors_total</c> (counter, reason-labeled), and
/// <c>clamd_scan_duration_seconds</c> (histogram).</para>
/// </remarks>
public sealed class ClamdMediaScanner : IMediaScanner, IDisposable
{
    private const int ChunkSize = 64 * 1024;
    private const string MeterName = "FinalProject.Application";

    private static readonly Meter _meter = new(MeterName);
    private static readonly Counter<long> _scansTotal = _meter.CreateCounter<long>(
        "clamd_scans_total",
        unit: "{scan}",
        description: "Total clamd scans completed, labeled by verdict.");
    private static readonly Counter<long> _quarantinesTotal = _meter.CreateCounter<long>(
        "clamd_quarantines_total",
        unit: "{quarantine}",
        description: "Quarantine events, labeled by detected signature.");
    private static readonly Counter<long> _errorsTotal = _meter.CreateCounter<long>(
        "clamd_errors_total",
        unit: "{error}",
        description: "Scanner errors, labeled by reason (timeout | unreachable | unexpected).");
    private static readonly Histogram<double> _scanDuration = _meter.CreateHistogram<double>(
        "clamd_scan_duration_seconds",
        unit: "s",
        description: "Wall-clock duration of a clamd scan, including connect.");

    private readonly MediaScannerOptions _options;
    private readonly ILogger<ClamdMediaScanner> _logger;
    private readonly SemaphoreSlim _concurrencyGate;

    public ClamdMediaScanner(IOptions<MediaScannerOptions> options, ILogger<ClamdMediaScanner> logger)
    {
        _options = options.Value;
        _logger = logger;
        int max = Math.Max(1, _options.MaxConcurrentScans);
        _concurrencyGate = new SemaphoreSlim(max, max);
    }

    public async Task<MediaScanResult> ScanAsync(Stream payload, string contentType, CancellationToken cancellationToken = default)
    {
        // Wait outside the timeout window — connect/scan timeouts measure clamd round-trip time,
        // not the caller-side queueing latency. Otherwise a busy scanner under load would
        // mis-report timeouts when reality was "we waited 4s for a free connection."
        await _concurrencyGate.WaitAsync(cancellationToken);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            using CancellationTokenSource connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds));
            await client.ConnectAsync(_options.Host, _options.Port, connectCts.Token);

            using NetworkStream stream = client.GetStream();

            // Use the null-terminated zINSTREAM dialect so the response is read as plain text up
            // to the next \0 — easier to parse and clamd-recommended for new clients.
            await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), cancellationToken);

            byte[] buffer = new byte[ChunkSize];
            int bytesRead;
            using CancellationTokenSource scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            scanCts.CancelAfter(TimeSpan.FromSeconds(_options.ScanTimeoutSeconds));

            while ((bytesRead = await payload.ReadAsync(buffer.AsMemory(0, ChunkSize), scanCts.Token)) > 0)
            {
                // Per chunk: big-endian uint32 length, then the bytes.
                byte[] lengthHeader = new byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(lengthHeader, (uint)bytesRead);
                await stream.WriteAsync(lengthHeader, scanCts.Token);
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), scanCts.Token);
            }

            // Zero-length frame signals end-of-stream to clamd.
            await stream.WriteAsync(new byte[] { 0, 0, 0, 0 }, scanCts.Token);

            // Read the single-line response.
            using var reader = new MemoryStream();
            int b;
            while ((b = stream.ReadByte()) > 0)
                reader.WriteByte((byte)b);
            string response = Encoding.ASCII.GetString(reader.ToArray()).Trim();

            MediaScanResult result = ClamdResponseParser.Parse(response);
            RecordResult(result, sw.Elapsed);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _errorsTotal.Add(1, new KeyValuePair<string, object?>("reason", "timeout"));
            return Indeterminate("clamd scan timed out", sw.Elapsed);
        }
        catch (Exception ex)
        {
            _errorsTotal.Add(1, new KeyValuePair<string, object?>("reason", "unreachable"));
            _logger.LogWarning(ex, "clamd scan failed: {Message}", ex.Message);
            return Indeterminate($"clamd unreachable: {ex.Message}", sw.Elapsed);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private void RecordResult(MediaScanResult result, TimeSpan elapsed)
    {
        // Verdict tag enables a single panel to chart Clean/Quarantine/Unknown rates with one
        // counter — better cardinality than three separate counters.
        _scansTotal.Add(1, new KeyValuePair<string, object?>("verdict", result.Verdict.ToString()));
        _scanDuration.Record(elapsed.TotalSeconds);

        if (result.Verdict == ScanVerdict.Quarantine)
        {
            // The signature name is bounded vocabulary (clamd's database has ~5M entries but a
            // production AV environment realistically sees <100 distinct hits/day) so it's safe
            // as a metric label without exploding cardinality.
            _quarantinesTotal.Add(1, new KeyValuePair<string, object?>("signature", ExtractSignature(result.Detail)));
        }
        else if (result.Verdict == ScanVerdict.Unknown)
        {
            _errorsTotal.Add(1, new KeyValuePair<string, object?>("reason", "unexpected"));
        }
    }

    private static string ExtractSignature(string detail)
    {
        // Format: "stream: Win.Test.EICAR_HDB-1 FOUND" → "Win.Test.EICAR_HDB-1"
        const string prefix = "stream: ";
        const string suffix = " FOUND";
        if (detail.StartsWith(prefix, StringComparison.Ordinal) && detail.EndsWith(suffix, StringComparison.Ordinal))
            return detail[prefix.Length..^suffix.Length];
        return "unknown";
    }

    private MediaScanResult Indeterminate(string detail, TimeSpan elapsed)
    {
        _scanDuration.Record(elapsed.TotalSeconds);
        return _options.FailClosed
            ? new MediaScanResult(ScanVerdict.Quarantine, $"fail-closed: {detail}")
            : new MediaScanResult(ScanVerdict.Unknown, detail);
    }

    public void Dispose() => _concurrencyGate.Dispose();
}
