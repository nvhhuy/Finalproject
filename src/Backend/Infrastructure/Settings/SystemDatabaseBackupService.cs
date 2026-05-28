using System.Diagnostics;
using Application.Contracts.Settings;
using Application.Contracts.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Settings;

public sealed class SystemDatabaseBackupService : ISystemDatabaseBackupService
{
    private const string BackupPrefix = "system-backups/database/";

    private readonly IObjectStorage _storage;
    private readonly ILogger<SystemDatabaseBackupService> _logger;
    private readonly DatabaseBackupOptions _options;
    private readonly string _connectionString;

    public SystemDatabaseBackupService(
        IObjectStorage storage,
        IConfiguration configuration,
        IOptions<DatabaseBackupOptions> options,
        ILogger<SystemDatabaseBackupService> logger)
    {
        _storage = storage;
        _logger = logger;
        _options = options.Value;
        _connectionString = configuration.GetConnectionString("DatabaseConnectionString")
            ?? throw new InvalidOperationException("ConnectionStrings:DatabaseConnectionString is required for database backup.");
    }

    public async Task<CreateSystemDatabaseBackupResult> CreateBackupAsync(string triggerSource, CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        string key = $"{BackupPrefix}{now:yyyyMMddHHmmssfff}-database.dump";
        string tempPath = Path.Combine(Path.GetTempPath(), $"baseproject-{Guid.NewGuid():N}.dump");
        try
        {
            NpgsqlConnectionStringBuilder builder = new(_connectionString);
            string host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
            string username = string.IsNullOrWhiteSpace(builder.Username) ? "postgres" : builder.Username;
            string database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database;
            string password = builder.Password ?? string.Empty;
            string args =
                $"--format=custom --no-owner --no-privileges --host={EscapeArg(host)} --port={builder.Port} --username={EscapeArg(username)} --dbname={EscapeArg(database)} --file={EscapeArg(tempPath)}";

            await RunProcessAsync(_options.PgDumpPath, args, password, cancellationToken);

            await using var stream = File.OpenRead(tempPath);
            await _storage.PutAsync(key, stream, "application/octet-stream", cancellationToken);
            long size = stream.Length;
            _logger.LogInformation("Database backup created ({Key}, {Bytes} bytes) via {Trigger}", key, size, triggerSource);
            return new CreateSystemDatabaseBackupResult(key, now, size);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public async Task<IReadOnlyList<SystemDatabaseBackupItem>> ListBackupsAsync(int take, CancellationToken cancellationToken = default)
    {
        int boundedTake = Math.Clamp(take, 1, 100);
        var entries = new List<SystemDatabaseBackupItem>(boundedTake);
        await foreach (ObjectListing obj in _storage.ListKeysAsync(BackupPrefix, cancellationToken))
            entries.Add(new SystemDatabaseBackupItem(obj.Key, obj.LastModifiedUtc, obj.ByteSize));

        return entries
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(boundedTake)
            .ToList();
    }

    public async Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(BackupPrefix, StringComparison.Ordinal))
            return false;

        string tempPath = Path.Combine(Path.GetTempPath(), $"baseproject-restore-{Guid.NewGuid():N}.dump");
        try
        {
            await using (Stream source = await _storage.GetAsync(key, cancellationToken))
            await using (FileStream target = File.Create(tempPath))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            NpgsqlConnectionStringBuilder builder = new(_connectionString);
            string host = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host;
            string username = string.IsNullOrWhiteSpace(builder.Username) ? "postgres" : builder.Username;
            string database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database;
            string password = builder.Password ?? string.Empty;
            string args =
                $"--clean --if-exists --no-owner --no-privileges --single-transaction --host={EscapeArg(host)} --port={builder.Port} --username={EscapeArg(username)} --dbname={EscapeArg(database)} {EscapeArg(tempPath)}";

            await RunProcessAsync(_options.PgRestorePath, args, password, cancellationToken);
            _logger.LogWarning("Database restore completed from backup {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database restore failed for backup {Key}", key);
            return false;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public async Task<int> CleanupExpiredBackupsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        int boundedDays = Math.Max(1, retentionDays);
        DateTime cutoffUtc = DateTime.UtcNow.AddDays(-boundedDays);
        int deleted = 0;
        await foreach (ObjectListing obj in _storage.ListKeysAsync(BackupPrefix, cancellationToken))
        {
            if (obj.LastModifiedUtc >= cutoffUtc)
                continue;

            await _storage.DeleteAsync(obj.Key, cancellationToken);
            deleted++;
        }

        return deleted;
    }

    private async Task RunProcessAsync(string fileName, string arguments, string password, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["PGPASSWORD"] = password;

        using var process = new Process { StartInfo = psi };
        process.Start();
        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{fileName} failed with exit code {process.ExitCode}. stderr: {stderr}\nstdout: {stdout}");
    }

    private static string EscapeArg(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
