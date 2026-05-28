namespace Application.Contracts.Settings;

public sealed record SystemDatabaseBackupItem(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record CreateSystemDatabaseBackupResult(string Key, DateTime CreatedAtUtc, long ByteSize);

public interface ISystemDatabaseBackupService
{
    Task<CreateSystemDatabaseBackupResult> CreateBackupAsync(string triggerSource, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemDatabaseBackupItem>> ListBackupsAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
