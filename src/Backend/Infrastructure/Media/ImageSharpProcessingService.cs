using Application.Contracts.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Media;

/// <summary>
///     <see cref="IImageProcessingService" /> backed by <c>SixLabors.ImageSharp</c>. Each operation
///     decodes the source once, applies EXIF auto-orientation (so iPhone portrait shots don't render
///     sideways), then re-encodes the output at the requested format + quality.
/// </summary>
/// <remarks>
///     Supported output codecs: JPEG (progressive) and WebP (lossy, q=quality). HEIC/AVIF outputs are
///     not enabled because ImageSharp 3.x has no native encoder for them — that would require a
///     libheif/libaom interop layer.
/// </remarks>
public sealed class ImageSharpProcessingService : IImageProcessingService
{
    public async Task<ProcessedImage> CreateThumbnailAsync(
        Stream source,
        int sizePx,
        ImageEncoding encoding,
        CancellationToken cancellationToken = default)
    {
        if (sizePx <= 0) throw new ArgumentOutOfRangeException(nameof(sizePx));

        using var image = await Image.LoadAsync(source, cancellationToken);
        image.Mutate(ctx => ctx
            .AutoOrient()
            .Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop, // centre-crop to a square
                Size = new Size(sizePx, sizePx),
                Sampler = KnownResamplers.Lanczos3
            }));

        return await EncodeAsync(image, encoding, cancellationToken);
    }

    public async Task<ProcessedImage> ResizeAsync(
        Stream source,
        int maxEdgePx,
        ImageEncoding encoding,
        CancellationToken cancellationToken = default)
    {
        if (maxEdgePx <= 0) throw new ArgumentOutOfRangeException(nameof(maxEdgePx));

        using var image = await Image.LoadAsync(source, cancellationToken);
        image.Mutate(ctx => ctx.AutoOrient());

        var needsResize = image.Width > maxEdgePx || image.Height > maxEdgePx;
        if (needsResize)
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max, // fit inside the box, preserve aspect ratio
                Size = new Size(maxEdgePx, maxEdgePx),
                Sampler = KnownResamplers.Lanczos3
            }));

        return await EncodeAsync(image, encoding, cancellationToken);
    }

    public async Task<ImageProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var info = await Image.IdentifyAsync(source, cancellationToken);
        return new ImageProbeResult(info.Width, info.Height, info.Metadata.DecodedImageFormat?.Name ?? "unknown");
    }

    private static async Task<ProcessedImage> EncodeAsync(Image image, ImageEncoding encoding, CancellationToken ct)
    {
        var encoder = BuildEncoder(encoding);
        var output = new MemoryStream();
        await image.SaveAsync(output, encoder, ct);
        output.Position = 0;
        return new ProcessedImage(output, encoding.ContentType, image.Width, image.Height, output.Length);
    }

    private static IImageEncoder BuildEncoder(ImageEncoding encoding)
    {
        return encoding.Format switch
        {
            ImageFormat.Jpeg => new JpegEncoder { Quality = Clamp(encoding.Quality), Interleaved = true },
            ImageFormat.Webp => new WebpEncoder
            {
                Quality = Clamp(encoding.Quality),
                FileFormat = WebpFileFormatType.Lossy,
                Method = WebpEncodingMethod.Default // good speed/size tradeoff for worker throughput
            },
            _ => throw new InvalidOperationException($"Unsupported image format {encoding.Format}.")
        };
    }

    private static int Clamp(int quality)
    {
        return Math.Clamp(quality, 1, 100);
    }
}