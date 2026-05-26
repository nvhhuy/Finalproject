namespace Domains.MediaAggregate;

/// <summary>
/// A single processed output produced from <see cref="MediaAsset.OriginalKey"/>. Variants are
/// owned by the <see cref="MediaAsset"/> aggregate root — they don't have their own repository
/// and must be reached via the parent.
/// </summary>
public sealed class MediaVariant
{
    private MediaVariant() { } // EF

    private MediaVariant(
        Guid id,
        MediaVariantKind kind,
        string storageKey,
        string contentType,
        long byteSize,
        int? width,
        int? height,
        TimeSpan? duration)
    {
        Id = id;
        Kind = kind;
        StorageKey = storageKey;
        ContentType = contentType;
        ByteSize = byteSize;
        Width = width;
        Height = height;
        Duration = duration;
    }

    /// <summary>Creates a new variant attached to a parent asset. <paramref name="storageKey"/> must already exist in the bucket.</summary>
    internal static MediaVariant Create(
        MediaVariantKind kind,
        string storageKey,
        string contentType,
        long byteSize,
        int? width = null,
        int? height = null,
        TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key cannot be empty.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        if (byteSize < 0)
            throw new ArgumentOutOfRangeException(nameof(byteSize));

        return new MediaVariant(Guid.NewGuid(), kind, storageKey, contentType, byteSize, width, height, duration);
    }

    public Guid Id { get; private set; }
    public MediaVariantKind Kind { get; private set; }

    /// <summary>Object key in the bucket. Combine with <c>IObjectStorage.GetPublicUrl</c> to render.</summary>
    public string StorageKey { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;
    public long ByteSize { get; private set; }

    /// <summary>Pixel width — null for non-visual variants.</summary>
    public int? Width { get; private set; }
    public int? Height { get; private set; }

    /// <summary>Duration for video variants. Null for images.</summary>
    public TimeSpan? Duration { get; private set; }
}
