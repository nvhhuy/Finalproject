using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Application.Contracts.Storage;

namespace Infrastructure.Storage;

/// <summary>
///     Process-local object storage for integration tests and environments without S3.
/// </summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, StoredObject> _objects = new(StringComparer.Ordinal);

    public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _objects[key] = new StoredObject(buffer.ToArray(), contentType, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key, out var stored))
            throw new FileNotFoundException($"Object not found: {key}");

        return Task.FromResult<Stream>(new MemoryStream(stored.Bytes, false));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key)
    {
        return $"inmemory:///{key}";
    }

    public Task<string> GeneratePresignedDownloadUrlAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetPublicUrl(key));
    }

    public Task<string> GeneratePresignedUploadUrlAsync(
        string key,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetPublicUrl(key));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_objects.ContainsKey(key));
    }

    public Task<ObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(key, out var stored))
            return Task.FromResult<ObjectMetadata?>(null);

        return Task.FromResult<ObjectMetadata?>(new ObjectMetadata(stored.Bytes.LongLength, stored.ContentType));
    }

    public async IAsyncEnumerable<ObjectListing> ListKeysAsync(
        string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (key, stored) in _objects)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            yield return new ObjectListing(key, stored.Bytes.LongLength, stored.LastModifiedUtc);
            await Task.Yield();
        }
    }

    private sealed record StoredObject(byte[] Bytes, string ContentType, DateTime LastModifiedUtc);
}