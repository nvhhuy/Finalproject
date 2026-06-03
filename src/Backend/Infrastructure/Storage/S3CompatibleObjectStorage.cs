using System.Net;
using System.Runtime.CompilerServices;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Application.Contracts.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>
///     S3 v4 storage backend that talks to AWS S3, Cloudflare R2, MinIO or any other compatible service
///     through a single set of credentials. The instance is registered as a singleton because
///     <see cref="IAmazonS3" /> is thread-safe and cheap to share across the host.
/// </summary>
public sealed class S3CompatibleObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly ILogger<S3CompatibleObjectStorage> _logger;
    private readonly ObjectStorageOptions _options;

    public S3CompatibleObjectStorage(IOptions<ObjectStorageOptions> options, ILogger<S3CompatibleObjectStorage> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ForcePathStyle = _options.ForcePathStyle,
            // R2/MinIO require pre-signed v4; AWS supports it everywhere.
            SignatureVersion = "4",
            // Fail fast in CI/integration tests when object storage is unreachable (avoid 15m job timeouts).
            Timeout = TimeSpan.FromSeconds(60)
        };

        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
            config.ServiceURL = _options.ServiceUrl;
        else
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);

        _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    public async Task PutAsync(string key, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            // AutoCloseStream=false so callers retain ownership; we never want the SDK to dispose
            // a stream the application is still managing.
            AutoCloseStream = false,
            DisablePayloadSigning = true // R2 rejects payload signing on PUT for streamed bodies
        };

        await _client.PutObjectAsync(request, cancellationToken);
        _logger.LogDebug("Uploaded {Key} ({ContentType}) to bucket {Bucket}", key, contentType, _options.Bucket);
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetObjectAsync(_options.Bucket, key, cancellationToken);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
        _logger.LogDebug("Deleted {Key} from bucket {Bucket}", key, _options.Bucket);
    }

    public string GetPublicUrl(string key)
    {
        // Private-bucket / paid-content mode: hand back a short-lived signed URL. Done
        // synchronously through the AWS SDK's URL builder — no network call, just HMAC.
        if (_options.UsePresignedDownloads)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(_options.PresignedDownloadUrlExpirationMinutes)
            };
            return _client.GetPreSignedURL(request);
        }

        if (!string.IsNullOrWhiteSpace(_options.CdnBaseUrl))
            return _options.CdnBaseUrl.TrimEnd('/') + "/" + key.TrimStart('/');

        // No CDN configured: build the bucket-direct URL. Works for AWS path-style/virtual-hosted
        // and MinIO. R2 buckets have no public endpoint without a CDN — operators should configure
        // CdnBaseUrl to point at the worker route or a Cloudflare custom domain.
        if (!string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            var root = _options.ServiceUrl.TrimEnd('/');
            return _options.ForcePathStyle
                ? $"{root}/{_options.Bucket}/{key.TrimStart('/')}"
                : $"{root.Replace("//", $"//{_options.Bucket}.", StringComparison.Ordinal)}/{key.TrimStart('/')}";
        }

        return $"https://{_options.Bucket}.s3.{_options.Region}.amazonaws.com/{key.TrimStart('/')}";
    }

    public Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration)
        };

        return Task.FromResult(_client.GetPreSignedURL(request));
    }

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = contentType
        };

        var url = _client.GetPreSignedURL(request);
        return Task.FromResult(url);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<ObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var meta = await _client.GetObjectMetadataAsync(_options.Bucket, key, cancellationToken);
            return new ObjectMetadata(
                meta.ContentLength,
                meta.Headers.ContentType ?? "application/octet-stream");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<ObjectListing> ListKeysAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // S3 ListObjectsV2 returns at most 1000 entries per call and uses a continuation token for
        // paging. Yielding inside the loop streams results to the caller — no million-row buffer.
        string? continuationToken = null;
        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken,
                MaxKeys = 1000
            };
            var response = await _client.ListObjectsV2Async(request, cancellationToken);
            foreach (var obj in response.S3Objects ?? Enumerable.Empty<S3Object>())
                yield return new ObjectListing(
                    obj.Key,
                    obj.Size,
                    obj.LastModified.ToUniversalTime());
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        } while (continuationToken is not null);
    }
}