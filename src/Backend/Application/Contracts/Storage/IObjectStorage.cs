namespace Application.Contracts.Storage;

/// <summary>
///     Storage abstraction for binary blobs (media originals, processed variants, exports). Implemented
///     by <c>S3CompatibleObjectStorage</c> against any S3 v4 endpoint — AWS S3, Cloudflare R2, MinIO,
///     or other compatible services.
/// </summary>
/// <remarks>
///     The API host writes the original via <see cref="PutAsync" /> (or hands the client a presigned
///     URL via <see cref="GeneratePresignedUploadUrlAsync" />); the media worker downloads with
///     <see cref="GetAsync" />, produces variants, and writes them back. Public consumption uses
///     <see cref="GetPublicUrl" /> which routes through the CDN when one is configured.
/// </remarks>
public interface IObjectStorage
{
    /// <summary>Uploads <paramref name="content" /> to <paramref name="key" />. Overwrites if it already exists.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Downloads the object. Caller disposes the returned stream.</summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a single object. Idempotent — missing keys are not an error.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the consumer-facing URL for an object. Selection rules:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 If the storage implementation is configured for <b>presigned downloads</b>, a short-lived
    ///                 signed URL is generated (private bucket / paid-content scenarios).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>Else, when a CDN host is configured, returns <c>{CdnBaseUrl}/{key}</c>.</description>
    ///         </item>
    ///         <item>
    ///             <description>Else, falls back to the bucket-direct HTTPS URL.</description>
    ///         </item>
    ///     </list>
    /// </summary>
    /// <remarks>
    ///     Implementations may pre-cache or short-circuit URL construction; <see cref="GetPublicUrl" />
    ///     is called once per variant when projecting a DTO so it must stay cheap. Presigned-mode
    ///     callers should pass a sensible TTL — see <see cref="GeneratePresignedDownloadUrlAsync" />.
    /// </remarks>
    string GetPublicUrl(string key);

    /// <summary>
    ///     Generates a time-limited GET URL for a private object. Used when the bucket has no public
    ///     access (paid content, per-user content, regulated assets) — the API hands the URL to the
    ///     client who fetches the object directly from S3/CDN without proxying through the API host.
    /// </summary>
    /// <param name="key">Object key.</param>
    /// <param name="expiration">How long the URL remains valid. Keep short (minutes) to limit blast radius if leaked.</param>
    Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>Generates a time-limited PUT URL so a client can upload directly to the bucket.</summary>
    /// <param name="key">Destination object key (must be unique per upload).</param>
    /// <param name="contentType">Pre-declared <c>Content-Type</c> the client must send.</param>
    /// <param name="expiration">How long the URL remains valid.</param>
    Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>Returns <c>true</c> if the object exists in the bucket.</summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetches the storage-side metadata for an object (size, content-type, ETag) without
    ///     downloading the payload. Used by upload-completion to verify the client's declared size
    ///     against the real bytes the bucket received — without this we'd trust the unauthenticated
    ///     declared <c>ByteSize</c> for billing/quota.
    /// </summary>
    /// <returns><c>null</c> when the key is absent; non-null when the metadata was retrieved.</returns>
    Task<ObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists every object key under <paramref name="prefix" />, paginating internally. Used by the
    ///     orphan-cleanup job to find files that exist in the bucket but have no corresponding row
    ///     in the database (failed uploads, abandoned multipart uploads, retired keys after a layout
    ///     migration).
    /// </summary>
    /// <param name="prefix">Bucket prefix to scope the listing (e.g. <c>media/</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    ///     Returns an <see cref="IAsyncEnumerable{T}" /> so callers don't materialise a million-key
    ///     result into memory. The S3 ListObjectsV2 API returns at most 1000 keys per page; the
    ///     implementation hides that pagination behind the iterator.
    /// </remarks>
    IAsyncEnumerable<ObjectListing> ListKeysAsync(string prefix, CancellationToken cancellationToken = default);
}

/// <summary>Storage-side metadata returned by <see cref="IObjectStorage.GetMetadataAsync" />.</summary>
/// <param name="ByteSize">Authoritative size in bytes as reported by the bucket.</param>
/// <param name="ContentType">Declared content type stored by the bucket (may not match the actual bytes).</param>
public sealed record ObjectMetadata(long ByteSize, string ContentType);

/// <summary>One entry from <see cref="IObjectStorage.ListKeysAsync" />.</summary>
/// <param name="Key">Full object key (includes prefix).</param>
/// <param name="ByteSize">Size in bytes.</param>
/// <param name="LastModifiedUtc">When the bucket last wrote the object.</param>
public sealed record ObjectListing(string Key, long ByteSize, DateTime LastModifiedUtc);