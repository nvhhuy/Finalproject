using System.Diagnostics;
using Api.Middleware;

namespace Api.Models;

/// <summary>
///     Optional metadata on every API payload; extend with new properties or use <see cref="Extensions" /> for ad-hoc
///     keys.
/// </summary>
public sealed record ApiMeta
{
    public string? TraceId { get; init; }

    /// <summary>Echo of <c>X-Correlation-Id</c> when <see cref="CorrelationIdMiddleware" /> is enabled.</summary>
    public string? CorrelationId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Client- or gateway-specific extensions (correlation id, feature flags, etc.).</summary>
    public IReadOnlyDictionary<string, object>? Extensions { get; init; }

    public static ApiMeta FromHttpContext(HttpContext? httpContext)
    {
        var trace = httpContext?.TraceIdentifier ?? Activity.Current?.Id;
        var correlationId = httpContext?.Items.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var cid) ==
                            true
            ? cid as string
            : null;
        return new ApiMeta
        {
            TraceId = trace,
            CorrelationId = correlationId,
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }
}