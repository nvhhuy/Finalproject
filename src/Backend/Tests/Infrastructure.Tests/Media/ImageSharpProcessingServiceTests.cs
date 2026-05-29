using System.Numerics;
using System.Text;
using Application.Contracts.Media;
using Infrastructure.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Tests.Media;

/// <summary>
///     End-to-end exercises of the ImageSharp processing service. We feed it a small synthetic JPEG
///     generated on the fly so the tests have no dependency on external sample files.
/// </summary>
public sealed class ImageSharpProcessingServiceTests
{
    private readonly ImageSharpProcessingService _service = new();

    [Fact]
    public async Task CreateThumbnail_produces_square_jpeg_at_requested_size()
    {
        await using Stream source = CreateSyntheticJpeg(800, 600);

        var thumb = await _service.CreateThumbnailAsync(source, 256, ImageEncoding.Jpeg(80));

        Assert.Equal("image/jpeg", thumb.ContentType);
        Assert.Equal(256, thumb.Width);
        Assert.Equal(256, thumb.Height);
        Assert.True(thumb.ByteSize > 0);
        Assert.True(thumb.Content.CanRead);

        thumb.Content.Position = 0;
        using var decoded = await Image.LoadAsync(thumb.Content);
        Assert.Equal(256, decoded.Width);
        Assert.Equal(256, decoded.Height);
    }

    [Fact]
    public async Task CreateThumbnail_supports_webp_output()
    {
        await using Stream source = CreateSyntheticJpeg(800, 600);

        var thumb = await _service.CreateThumbnailAsync(source, 96, ImageEncoding.Webp(70));

        Assert.Equal("image/webp", thumb.ContentType);
        Assert.Equal(96, thumb.Width);
        Assert.Equal(96, thumb.Height);

        // WebP magic bytes: "RIFF" .... "WEBP"
        thumb.Content.Position = 0;
        var header = new byte[12];
        Assert.Equal(12, await thumb.Content.ReadAsync(header.AsMemory(0, 12)));
        Assert.Equal("RIFF", Encoding.ASCII.GetString(header, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(header, 8, 4));
    }

    [Fact]
    public async Task Resize_caps_max_edge_and_preserves_aspect_ratio()
    {
        await using Stream source = CreateSyntheticJpeg(2000, 1000);

        var resized = await _service.ResizeAsync(source, 1000, ImageEncoding.Jpeg(82));

        Assert.Equal(1000, resized.Width);
        Assert.Equal(500, resized.Height);

        resized.Content.Position = 0;
        using var decoded = await Image.LoadAsync(resized.Content);
        Assert.Equal(1000, decoded.Width);
        Assert.Equal(500, decoded.Height);
    }

    [Fact]
    public async Task Resize_does_not_upsample_smaller_images()
    {
        await using Stream source = CreateSyntheticJpeg(300, 200);

        var resized = await _service.ResizeAsync(source, 1000, ImageEncoding.Jpeg(82));

        Assert.Equal(300, resized.Width);
        Assert.Equal(200, resized.Height);
    }

    [Fact]
    public async Task Resize_auto_orients_image_with_exif_rotation()
    {
        // EXIF orientation 6 = "rotate 90° clockwise on render". A naive resize without AutoOrient
        // would keep the bytes in the wrong orientation; we verify the output reports the rotated
        // dimensions (height/width swapped vs the raw source) which proves AutoOrient ran.
        await using Stream source = CreateSyntheticJpegWithExifOrientation(800, 400, 6);

        var resized = await _service.ResizeAsync(source, 1000, ImageEncoding.Jpeg(82));

        // After auto-orient, the 800×400 landscape source becomes a 400×800 portrait. Both edges are
        // under the 1000 cap, so dimensions are preserved exactly.
        Assert.Equal(400, resized.Width);
        Assert.Equal(800, resized.Height);
    }

    [Fact]
    public async Task Probe_returns_decoded_dimensions()
    {
        await using Stream source = CreateSyntheticJpeg(640, 480);

        var probe = await _service.ProbeAsync(source);

        Assert.Equal(640, probe.Width);
        Assert.Equal(480, probe.Height);
        Assert.False(string.IsNullOrWhiteSpace(probe.Format));
    }

    /// <summary>
    ///     Produces an in-memory JPEG of the given size. Uses a deterministic gradient so any compression
    ///     artefacts are stable across runs and reviewers can eyeball the variant if a test ever fails.
    /// </summary>
    private static MemoryStream CreateSyntheticJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx =>
        {
            ctx.ProcessPixelRowsAsVector4(row =>
            {
                for (var x = 0; x < row.Length; x++)
                    row[x] = new Vector4(x / (float)row.Length, 0.5f, 1f, 1f);
            });
        });

        var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 80 });
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream CreateSyntheticJpegWithExifOrientation(int width, int height, ushort orientation)
    {
        using var image = new Image<Rgba32>(width, height);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, orientation);
        image.Metadata.ExifProfile = exif;

        var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 80 });
        ms.Position = 0;
        return ms;
    }
}