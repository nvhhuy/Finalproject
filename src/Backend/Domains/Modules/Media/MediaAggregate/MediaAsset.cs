using Domains.Common;
using Domains.MediaAggregate.Events;
using Domains.MediaAggregate.ValueObjects;

namespace Domains.MediaAggregate;

/// <summary>
/// Aggregate root representing a single media upload (image or video) and every processed variant
/// derived from it. The asset moves through <see cref="MediaAssetStatus"/> Pending → Uploaded →
/// Processing → Ready, or terminates in <see cref="MediaAssetStatus.Failed"/> after exhausted
/// retries. All transitions raise domain events so the outbox + worker pipeline can react.
/// </summary>
public sealed class MediaAsset : AggregateRoot<MediaAssetId>
{
    private readonly List<MediaVariant> _variants = new();

    public Guid OwnerUserId { get; private set; }
    public MediaKind Kind { get; private set; }
    public MediaAssetStatus Status { get; private set; }

    /// <summary>Storage key of the original upload. Bucket key, NOT a URL.</summary>
    public string OriginalKey { get; private set; } = null!;

    /// <summary>MIME of the original upload as declared by the client.</summary>
    public string ContentType { get; private set; } = null!;

    public long ByteSize { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the client confirmed the upload finished.</summary>
    public DateTime? UploadedAtUtc { get; private set; }

    /// <summary>
    /// When the worker most recently called <see cref="MarkProcessing"/>. Used by the stale-recovery
    /// job to detect zombie workers that grabbed an asset and never finished — if this timestamp
    /// is older than the configured threshold and <see cref="Status"/> is still <c>Processing</c>,
    /// the asset is requeued.
    /// </summary>
    public DateTime? ProcessingStartedAtUtc { get; private set; }

    /// <summary>
    /// Number of times <see cref="MarkProcessing"/> has been called on this asset. Used to break
    /// poison-message loops: if a corrupt input keeps killing the worker (OOM / ffmpeg infinite
    /// loop), stale-recovery would otherwise resurrect it forever. After
    /// <c>MaxProcessingAttempts</c> the recovery job gives up and transitions to <c>Failed</c>
    /// permanently — operator intervention required from there.
    /// </summary>
    public int ProcessingAttemptCount { get; private set; }

    /// <summary>When processing succeeded.</summary>
    public DateTime? ProcessedAtUtc { get; private set; }

    /// <summary>Last error message from the processing pipeline (truncated). Set when <see cref="Status"/> = <see cref="MediaAssetStatus.Failed"/>.</summary>
    public string? ErrorMessage { get; private set; }

    public IReadOnlyCollection<MediaVariant> Variants => _variants.AsReadOnly();

    private MediaAsset() : base() { }

    private MediaAsset(
        MediaAssetId id,
        Guid ownerUserId,
        MediaKind kind,
        string originalKey,
        string contentType,
        long byteSize,
        DateTime createdAtUtc) : base(id)
    {
        OwnerUserId = ownerUserId;
        Kind = kind;
        OriginalKey = originalKey;
        ContentType = contentType;
        ByteSize = byteSize;
        Status = MediaAssetStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Reserves a new asset row before the bytes are uploaded. The caller hands the returned
    /// <see cref="OriginalKey"/> back to the client as part of a presigned URL or stream sink.
    /// </summary>
    /// <param name="byteSize">
    /// Best-effort upload size declared by the client. The worker re-reads the real size from the
    /// bucket once the upload completes and may reject the asset if the declared size is wildly
    /// different (anti-abuse).
    /// </param>
    public static MediaAsset CreateForUpload(
        Guid ownerUserId,
        MediaKind kind,
        string originalKey,
        string contentType,
        long byteSize,
        DateTime utcNow) =>
        CreateForUpload(MediaAssetId.CreateNew(), ownerUserId, kind, originalKey, contentType, byteSize, utcNow);

    /// <summary>
    /// Same as <see cref="CreateForUpload(Guid, MediaKind, string, string, long, DateTime)"/> but
    /// lets the caller pre-allocate the id. Used by the upload command so the storage key
    /// (which embeds the id) is known before the row exists.
    /// </summary>
    public static MediaAsset CreateForUpload(
        MediaAssetId id,
        Guid ownerUserId,
        MediaKind kind,
        string originalKey,
        string contentType,
        long byteSize,
        DateTime utcNow)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        if (string.IsNullOrWhiteSpace(originalKey))
            throw new ArgumentException("Original key is required.", nameof(originalKey));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));
        if (byteSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteSize), "Byte size must be positive.");

        return new MediaAsset(id, ownerUserId, kind, originalKey, contentType, byteSize, utcNow);
    }

    /// <summary>
    /// Marks the upload as completed. Raises <see cref="MediaAssetUploadedDomainEvent"/> so the
    /// outbox publisher forwards it to the worker queue.
    /// </summary>
    /// <param name="utcNow">UTC timestamp recorded as <see cref="UploadedAtUtc"/>.</param>
    /// <param name="verifiedByteSize">
    /// Optional authoritative size in bytes, fetched from object storage HEAD. When supplied,
    /// overrides the client-declared <see cref="ByteSize"/> so quota and billing use the real
    /// value rather than what an unauthenticated client claimed at initiate-time.
    /// </param>
    public void MarkUploaded(DateTime utcNow, long? verifiedByteSize = null)
    {
        if (Status != MediaAssetStatus.Pending)
            throw new InvalidOperationException($"Cannot mark uploaded from status '{Status}'. Expected Pending.");
        if (verifiedByteSize is { } v && v <= 0)
            throw new ArgumentOutOfRangeException(nameof(verifiedByteSize), "Verified byte size must be positive when supplied.");

        Status = MediaAssetStatus.Uploaded;
        UploadedAtUtc = utcNow;
        if (verifiedByteSize is { } size)
            ByteSize = size;

        AddDomainEvent(new MediaAssetUploadedDomainEvent(Id.Value, OwnerUserId, Kind, OriginalKey, ContentType, ByteSize));
    }

    /// <summary>
    /// Worker took ownership of the asset and started producing variants. The supplied timestamp
    /// is stored as <see cref="ProcessingStartedAtUtc"/> so the stale-recovery job can detect runs
    /// that get wedged (worker OOM, container kill, infinite ffmpeg loop). Increments
    /// <see cref="ProcessingAttemptCount"/> so we can put a cap on retry loops for poison inputs.
    /// </summary>
    public void MarkProcessing(DateTime utcNow)
    {
        if (Status is not (MediaAssetStatus.Uploaded or MediaAssetStatus.Failed))
            throw new InvalidOperationException($"Cannot start processing from status '{Status}'.");

        Status = MediaAssetStatus.Processing;
        ProcessingStartedAtUtc = utcNow;
        ProcessingAttemptCount++;
        ErrorMessage = null;
    }

    /// <summary>
    /// Resets a wedged asset back to <see cref="MediaAssetStatus.Uploaded"/> so the worker pool will
    /// pick it up again. Called by the stale-recovery cron job when an asset has been in
    /// <see cref="MediaAssetStatus.Processing"/> longer than the configured threshold.
    /// </summary>
    /// <param name="utcNow">Current UTC time, used to refresh <see cref="UploadedAtUtc"/>.</param>
    /// <param name="maxAttempts">
    /// Upper bound on <see cref="ProcessingAttemptCount"/>. When the count has reached this
    /// threshold, the asset is parked in <see cref="MediaAssetStatus.Failed"/> instead of being
    /// requeued — this breaks poison-message loops where a corrupt file repeatedly crashes the
    /// worker mid-pipeline.
    /// </param>
    /// <remarks>
    /// <para>Drops any partial variants from the previous run — encoding is idempotent (output keys
    /// embed the rung height) so retried encodes will overwrite the storage objects, but the DB
    /// rows must be cleared to keep the variant projection accurate and avoid spurious duplicates
    /// in the public API surface.</para>
    /// <para>On retry, raises <see cref="MediaAssetUploadedDomainEvent"/> so the outbox publisher
    /// republishes to the worker queue. Re-queuing through the same channel as the original
    /// upload keeps the pipeline path single — there's no separate "retry" code path to maintain.</para>
    /// <para>When the attempt budget is exhausted, no event is raised. The asset stays in
    /// <see cref="MediaAssetStatus.Failed"/> with an explanatory <see cref="ErrorMessage"/> so an
    /// operator can investigate (corrupt file, unsupported codec, malicious payload).</para>
    /// </remarks>
    public void RequeueAfterStall(DateTime utcNow, int maxAttempts)
    {
        if (Status != MediaAssetStatus.Processing)
            throw new InvalidOperationException($"Cannot requeue from status '{Status}'. Expected Processing.");
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be at least 1.");

        if (ProcessingAttemptCount >= maxAttempts)
        {
            // Permanent quarantine — the file consistently breaks the worker. Promoting to Failed
            // (instead of leaving in Processing) keeps the stale-recovery query free of poison
            // assets and exposes the failure to support tooling.
            Status = MediaAssetStatus.Failed;
            ProcessingStartedAtUtc = null;
            ErrorMessage = $"Processing aborted after {ProcessingAttemptCount} attempts. Likely a corrupt or unsupported source — manual intervention required.";
            return;
        }

        _variants.Clear();
        Status = MediaAssetStatus.Uploaded;
        ProcessingStartedAtUtc = null;
        UploadedAtUtc = utcNow;
        ErrorMessage = null;
        AddDomainEvent(new MediaAssetUploadedDomainEvent(Id.Value, OwnerUserId, Kind, OriginalKey, ContentType, ByteSize));
    }

    /// <summary>
    /// Attaches a processed variant to the asset. The asset stays in <see cref="MediaAssetStatus.Processing"/>
    /// until <see cref="MarkReady"/> is called — the pipeline may add several variants before declaring done.
    /// </summary>
    public MediaVariant AddVariant(
        MediaVariantKind kind,
        string storageKey,
        string contentType,
        long byteSize,
        int? width = null,
        int? height = null,
        TimeSpan? duration = null)
    {
        if (Status is MediaAssetStatus.Pending)
            throw new InvalidOperationException("Cannot add variants to an asset that has not been uploaded yet.");

        MediaVariant variant = MediaVariant.Create(kind, storageKey, contentType, byteSize, width, height, duration);
        _variants.Add(variant);
        return variant;
    }

    /// <summary>Processing completed successfully. Raises <see cref="MediaAssetReadyDomainEvent"/>.</summary>
    public void MarkReady(DateTime utcNow)
    {
        if (Status != MediaAssetStatus.Processing)
            throw new InvalidOperationException($"Cannot mark ready from status '{Status}'. Expected Processing.");
        if (_variants.Count == 0)
            throw new InvalidOperationException("Cannot mark ready without at least one variant.");

        Status = MediaAssetStatus.Ready;
        ProcessedAtUtc = utcNow;
        ErrorMessage = null;
        AddDomainEvent(new MediaAssetReadyDomainEvent(Id.Value, OwnerUserId));
    }

    /// <summary>Processing exhausted retries. Stores a truncated error for operator triage.</summary>
    public void MarkFailed(string errorMessage)
    {
        Status = MediaAssetStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Unknown processing error"
            : (errorMessage.Length <= 2000 ? errorMessage : errorMessage[..2000]);
    }
}
