namespace Infrastructure.Storage;

/// <summary>
///     Configuration for the S3-compatible object store. Same shape works for AWS S3, Cloudflare R2,
///     MinIO and Backblaze B2 — change <see cref="ServiceUrl" /> + <see cref="ForcePathStyle" /> per provider.
/// </summary>
/// <remarks>
///     Provider quirks worth knowing:
///     <list type="bullet">
///         <item>
///             <description>
///                 <b>AWS S3</b> — leave <see cref="ServiceUrl" /> null and set <see cref="Region" />;
///                 <see cref="ForcePathStyle" /> = false.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Cloudflare R2</b> — <see cref="ServiceUrl" /> =
///                 <c>https://&lt;account&gt;.r2.cloudflarestorage.com</c>; <see cref="ForcePathStyle" /> = false;
///                 <see cref="Region" /> = <c>auto</c>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>MinIO</b> — <see cref="ServiceUrl" /> = <c>http://minio:9000</c>;
///                 <see cref="ForcePathStyle" /> = true.
///             </description>
///         </item>
///     </list>
/// </remarks>
public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>Custom endpoint (R2/MinIO). Leave empty to use AWS S3 default for <see cref="Region" />.</summary>
    public string? ServiceUrl { get; init; }

    /// <summary>AWS region or provider-specific value (R2 expects <c>auto</c>).</summary>
    public string Region { get; init; } = "us-east-1";

    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Bucket every object lives in. Created out-of-band via IaC, this app does not auto-create.</summary>
    public string Bucket { get; init; } = "media";

    /// <summary>MinIO requires path-style; AWS/R2 use virtual-hosted style.</summary>
    public bool ForcePathStyle { get; init; }

    /// <summary>
    ///     Optional CDN host that fronts the bucket (e.g. <c>https://cdn.example.com</c>). When set,
    ///     <c>GetPublicUrl</c> returns <c>{CdnBaseUrl}/{key}</c> instead of the raw bucket URL so reads
    ///     hit the cache layer. Leave null in dev.
    /// </summary>
    public string? CdnBaseUrl { get; init; }

    /// <summary>Default lifetime for presigned URLs handed to clients for direct upload.</summary>
    public int PresignedUrlExpirationMinutes { get; init; } = 10;

    /// <summary>
    ///     When true, <c>GetPublicUrl</c> returns a signed GET URL with a short TTL instead of the
    ///     raw CDN/bucket URL. Enable for private buckets (paid content, regulated assets); leave off
    ///     for public bucket + CDN setups where any HTTPS GET should succeed without a signature.
    /// </summary>
    public bool UsePresignedDownloads { get; init; }

    /// <summary>
    ///     TTL applied to presigned download URLs when <see cref="UsePresignedDownloads" /> is true.
    ///     Default 15 minutes — long enough that a tab can reload without refreshing the URL, short
    ///     enough that a leaked URL expires before serious abuse.
    /// </summary>
    public int PresignedDownloadUrlExpirationMinutes { get; init; } = 15;
}