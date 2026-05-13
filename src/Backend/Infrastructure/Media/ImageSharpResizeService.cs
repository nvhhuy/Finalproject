using Application.Contracts.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Media;

public sealed class ImageSharpResizeService : IImageProcessingService
{
    public async Task<Stream> ResizeAsync(Stream source, int maxEdge, CancellationToken cancellationToken = default)
    {
        using Image image = await Image.LoadAsync(source, cancellationToken);
        int w = image.Width;
        int h = image.Height;
        if (w <= maxEdge && h <= maxEdge)
        {
            MemoryStream copy = new();
            await image.SaveAsPngAsync(copy, cancellationToken);
            copy.Position = 0;
            return copy;
        }

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxEdge, maxEdge)
        }));

        MemoryStream output = new();
        await image.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }
}
