using Application.Contracts.Media;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Media;

/// <summary>
///     No-op scanner: returns <see cref="ScanVerdict.Clean" /> for every payload. Default registration
///     when no AV/moderation backend is configured. Logs at debug level so deployment misconfigs
///     (forgetting to wire ClamAV) are visible in startup logs without spamming requests.
/// </summary>
public sealed class NullMediaScanner : IMediaScanner
{
    private readonly ILogger<NullMediaScanner> _logger;

    public NullMediaScanner(ILogger<NullMediaScanner> logger)
    {
        _logger = logger;
    }

    public Task<MediaScanResult> ScanAsync(Stream payload, string contentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Media scanner disabled — accepting {ContentType} without scan", contentType);
        return Task.FromResult(new MediaScanResult(ScanVerdict.Clean, "no scanner configured"));
    }
}