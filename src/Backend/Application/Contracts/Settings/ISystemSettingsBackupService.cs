namespace Application.Contracts.Settings;

public sealed record SystemSettingsBackupItem(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record CreateSystemSettingsBackupResult(string Key, DateTime CreatedAtUtc, long ByteSize);

public sealed record ImportSystemSettingsBackupResult(
    string Key,
    DateTime CreatedAtUtc,
    long ByteSize,
    int SettingsApplied);

public sealed record SystemBackupDownloadResult(byte[] Content, string ContentType, string FileName);

public interface ISystemSettingsBackupService
{
    Task<CreateSystemSettingsBackupResult> CreateBackupAsync(string triggerSource,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemSettingsBackupItem>> ListBackupsAsync(int take,
        CancellationToken cancellationToken = default);

    Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default);
    Task<SystemBackupDownloadResult?> DownloadBackupAsync(string key, CancellationToken cancellationToken = default);

    Task<ImportSystemSettingsBackupResult?> ImportAndRestoreBackupAsync(Stream content,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);
}