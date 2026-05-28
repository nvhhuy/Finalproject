namespace Application.Contracts.Settings;

public sealed record SystemSettingsBackupItem(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record CreateSystemSettingsBackupResult(string Key, DateTime CreatedAtUtc, long ByteSize);

public interface ISystemSettingsBackupService
{
    Task<CreateSystemSettingsBackupResult> CreateBackupAsync(string triggerSource, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemSettingsBackupItem>> ListBackupsAsync(int take, CancellationToken cancellationToken = default);
    Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
