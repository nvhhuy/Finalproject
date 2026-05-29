using Application.Contracts.Storage;
using Infrastructure.Storage;

namespace Infrastructure.Tests.Storage;

public sealed class InMemoryObjectStorageTests
{
    [Fact]
    public async Task Put_get_list_and_delete_round_trip()
    {
        var storage = new InMemoryObjectStorage();
        var payload = "hello-backup"u8.ToArray();

        await using (var write = new MemoryStream(payload))
        {
            await storage.PutAsync("system-backups/settings/a.json", write, "application/json");
        }

        Assert.True(await storage.ExistsAsync("system-backups/settings/a.json"));

        await using var read = await storage.GetAsync("system-backups/settings/a.json");
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        Assert.Equal(payload, buffer.ToArray());

        var listed = new List<ObjectListing>();
        await foreach (var item in storage.ListKeysAsync("system-backups/settings/"))
            listed.Add(item);

        Assert.Single(listed);
        Assert.Equal("system-backups/settings/a.json", listed[0].Key);
        Assert.Equal(payload.LongLength, listed[0].ByteSize);

        await storage.DeleteAsync("system-backups/settings/a.json");
        Assert.False(await storage.ExistsAsync("system-backups/settings/a.json"));
    }

    [Fact]
    public async Task Get_throws_when_key_is_missing()
    {
        var storage = new InMemoryObjectStorage();
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.GetAsync("missing-key"));
    }
}