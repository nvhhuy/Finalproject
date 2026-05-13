using Application.Contracts.Storage;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Infrastructure.Storage;

/// <summary>S3-compatible storage (AWS S3, Cloudflare R2, MinIO). Disabled when bucket is not configured.</summary>
public sealed class S3CompatibleObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly IAmazonS3? _client;

    public S3CompatibleObjectStorage(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ServiceUrl) || string.IsNullOrWhiteSpace(_options.AccessKey))
            return;

        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = _options.ForcePathStyle,
            AuthenticationRegion = _options.Region
        };
        _client = new AmazonS3Client(credentials, config);
    }

    public string GetPublicUrl(string bucket, string key) =>
        string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{_options.ServiceUrl?.TrimEnd('/')}/{bucket}/{key}"
            : $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";

    public async Task<string> PutObjectAsync(string bucket, string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            throw new InvalidOperationException("Object storage is not configured.");

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            CannedACL = S3CannedACL.Private
        }, cancellationToken);

        return GetPublicUrl(bucket, key);
    }

    public async Task<Stream?> GetObjectAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return null;

        try
        {
            GetObjectResponse response = await _client.GetObjectAsync(bucket, key, cancellationToken);
            MemoryStream ms = new();
            await response.ResponseStream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            return ms;
        }
        catch (AmazonS3Exception)
        {
            return null;
        }
    }

    public async Task DeleteObjectAsync(string bucket, string key, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return;
        await _client.DeleteObjectAsync(bucket, key, cancellationToken);
    }
}

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";
    public string ServiceUrl { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Region { get; init; } = "auto";
    public bool ForcePathStyle { get; init; } = true;
    public string? PublicBaseUrl { get; init; }
}
