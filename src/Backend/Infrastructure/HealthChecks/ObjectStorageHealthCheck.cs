using Application.Contracts.Storage;
using Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Infrastructure.HealthChecks;

/// <summary>
///     Verifies the S3-compatible object store is reachable with valid credentials. Probes by issuing
///     a HEAD against a known sentinel key — a 404 is "healthy" (bucket reachable, sentinel just
///     doesn't exist), while connection refused / invalid signature / bucket-not-found surface as
///     unhealthy.
/// </summary>
/// <remarks>
///     <para>
///         Why a HEAD probe and not <c>ListBuckets</c>: some providers (Cloudflare R2 with restricted
///         IAM policies, MinIO with bucket-scoped credentials) only grant access to one bucket and reject
///         list-buckets. HEAD object is the lowest-privilege call that still exercises auth + DNS +
///         connectivity end-to-end.
///     </para>
///     <para>
///         The 404 case is treated as <b>Healthy</b> on purpose: a successful 404 means the S3 layer
///         is responding, our credentials work, and the bucket exists. The sentinel key is intentionally
///         not created — we don't want a health check that depends on bucket contents staying in sync
///         with deployment state.
///     </para>
/// </remarks>
public sealed class ObjectStorageHealthCheck : IHealthCheck
{
    private const string SentinelKey = ".health/object-storage-sentinel";
    private readonly ObjectStorageOptions _options;

    private readonly IObjectStorage _storage;

    public ObjectStorageHealthCheck(IObjectStorage storage, IOptions<ObjectStorageOptions> options)
    {
        _storage = storage;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Bucket))
            return HealthCheckResult.Degraded("ObjectStorage:Bucket not configured.");

        try
        {
            // ExistsAsync swallows 404 (returns false) which is exactly the signal we want for
            // "reachable but sentinel absent". Any other failure mode — DNS, auth, bucket missing,
            // network — propagates and we surface it as unhealthy.
            await _storage.ExistsAsync(SentinelKey, cancellationToken);
            return HealthCheckResult.Healthy(
                $"Object storage reachable (bucket={_options.Bucket}, endpoint={_options.ServiceUrl ?? _options.Region}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"Object storage unreachable (bucket={_options.Bucket}): {ex.Message}",
                ex);
        }
    }
}