using System.Text;
using Infrastructure.Settings;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Settings;

public sealed class SystemDatabaseBackupImportTests
{
    [Fact]
    public async Task ImportAndRestoreBackupAsync_rejects_non_pg_dump_content()
    {
        var storage = new InMemoryObjectStorage();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DatabaseConnectionString"] =
                    "Host=localhost;Port=5432;Database=baseproject_test;Username=postgres;Password=postgres"
            })
            .Build();

        var service = new SystemDatabaseBackupService(
            storage,
            configuration,
            Options.Create(new DatabaseBackupOptions { MaxDatabaseImportBytes = 1024 * 1024 }),
            NullLogger<SystemDatabaseBackupService>.Instance);

        await using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("not-a-pg-dump"));
        var result =
            await service.ImportAndRestoreBackupAsync(invalid, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadBackupAsync_returns_null_for_keys_outside_backup_prefix()
    {
        var storage = new InMemoryObjectStorage();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DatabaseConnectionString"] =
                    "Host=localhost;Port=5432;Database=baseproject_test;Username=postgres;Password=postgres"
            })
            .Build();

        var service = new SystemDatabaseBackupService(
            storage,
            configuration,
            Options.Create(new DatabaseBackupOptions()),
            NullLogger<SystemDatabaseBackupService>.Instance);

        var download =
            await service.DownloadBackupAsync("other-prefix/file.dump", CancellationToken.None);

        Assert.Null(download);
    }
}