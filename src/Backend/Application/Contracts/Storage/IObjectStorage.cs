namespace Application.Contracts.Storage;

public interface IObjectStorage
{
    Task<string> PutObjectAsync(string bucket, string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> GetObjectAsync(string bucket, string key, CancellationToken cancellationToken = default);
    Task DeleteObjectAsync(string bucket, string key, CancellationToken cancellationToken = default);
    string GetPublicUrl(string bucket, string key);
}
