namespace Application.Contracts.Settings;

public sealed record SystemDatabaseBackupItem(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record CreateSystemDatabaseBackupResult(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record ImportSystemDatabaseBackupResult(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record SystemDatabaseBackupDownloadResult(byte[] Content, string ContentType, string FileName);

public interface ISystemDatabaseBackupService
{
    Task<CreateSystemDatabaseBackupResult> CreateBackupAsync(string triggerSource, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemDatabaseBackupItem>> ListBackupsAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default);
    Task<SystemDatabaseBackupDownloadResult?> DownloadBackupAsync(string key, CancellationToken cancellationToken = default);
    Task<ImportSystemDatabaseBackupResult?> ImportAndRestoreBackupAsync(Stream content, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
