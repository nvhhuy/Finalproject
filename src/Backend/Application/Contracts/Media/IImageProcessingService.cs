namespace Application.Contracts.Media;

/// <summary>
/// Image processing operations. The contract is narrow so the pipeline only depends on operations
/// that all reasonable implementations support; format-specific tuning lives in the infrastructure
/// layer. Implementations are responsible for EXIF orientation handling — callers can rely on the
/// returned <see cref="ProcessedImage.Width"/>/<see cref="ProcessedImage.Height"/> matching what
/// the user actually sees.
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Produces a square thumbnail of the input bytes. Preserves aspect ratio via centre-crop so
    /// list rendering stays a uniform grid regardless of the source dimensions.
    /// </summary>
    Task<ProcessedImage> CreateThumbnailAsync(Stream source, int sizePx, ImageEncoding encoding, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aspect-preserving resize bounded by <paramref name="maxEdgePx"/>. If the source is already
    /// smaller in both dimensions, returns a re-encoded copy at the original size (so we never
    /// upsample).
    /// </summary>
    Task<ProcessedImage> ResizeAsync(Stream source, int maxEdgePx, ImageEncoding encoding, CancellationToken cancellationToken = default);

    /// <summary>Reads image dimensions without producing a derivative — used during ingest validation.</summary>
    Task<ImageProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default);
}

/// <summary>Container format + quality for an emitted image variant.</summary>
/// <param name="Format">Container/codec to encode with.</param>
/// <param name="Quality">
/// Encoder quality (0–100). Interpretation is codec-dependent — for JPEG ~80 is "high"; for WebP
/// the same visual quality is reached around 70 (WebP is ~30% smaller at equivalent perception).
/// </param>
public sealed record ImageEncoding(ImageFormat Format, int Quality)
{
    public static ImageEncoding Jpeg(int quality) => new(ImageFormat.Jpeg, quality);
    public static ImageEncoding Webp(int quality) => new(ImageFormat.Webp, quality);

    public string ContentType => Format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Webp => "image/webp",
        _ => throw new InvalidOperationException($"Unknown image format {Format}."),
    };

    public string FileExtension => Format switch
    {
        ImageFormat.Jpeg => "jpg",
        ImageFormat.Webp => "webp",
        _ => throw new InvalidOperationException($"Unknown image format {Format}."),
    };
}

public enum ImageFormat
{
    Jpeg = 1,
    Webp = 2,
}

/// <summary>Output of an image processing operation. The stream is positioned at 0 and the caller disposes it.</summary>
public sealed record ProcessedImage(Stream Content, string ContentType, int Width, int Height, long ByteSize);

public sealed record ImageProbeResult(int Width, int Height, string Format);
