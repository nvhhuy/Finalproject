namespace Application.Contracts.Media;

public interface IImageProcessingService
{
    /// <summary>Resize image to fit within maxEdge while preserving aspect ratio.</summary>
    Task<Stream> ResizeAsync(Stream source, int maxEdge, CancellationToken cancellationToken = default);
}
