using System.Text.Json;
using Application.Contracts.Settings;
using Application.Contracts.Storage;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Settings;

public sealed class SystemSettingsBackupService : ISystemSettingsBackupService
{
    private const string BackupPrefix = "system-backups/settings/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationReadDbContext _read;
    private readonly IObjectStorage _storage;
    private readonly IAppSettingsService _appSettingsService;

    public SystemSettingsBackupService(
        ApplicationReadDbContext read,
        IObjectStorage storage,
        IAppSettingsService appSettingsService)
    {
        _read = read;
        _storage = storage;
        _appSettingsService = appSettingsService;
    }

    public async Task<CreateSystemSettingsBackupResult> CreateBackupAsync(string triggerSource, CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        List<AppSettingSnapshotItem> items = await _read.AppSettings
            .AsNoTracking()
            .OrderBy(x => x.Key)
            .Select(x => new AppSettingSnapshotItem(x.Key, x.Value))
            .ToListAsync(cancellationToken);

        var payload = new SystemSettingsBackupPayload(
            Version: 1,
            TriggerSource: triggerSource,
            CreatedAtUtc: now,
            Items: items);

        string backupKey = $"{BackupPrefix}{now:yyyyMMddHHmmssfff}-settings.json";
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        await using var stream = new MemoryStream(json);
        await _storage.PutAsync(backupKey, stream, "application/json", cancellationToken);
        return new CreateSystemSettingsBackupResult(backupKey, now, json.LongLength);
    }

    public async Task<IReadOnlyList<SystemSettingsBackupItem>> ListBackupsAsync(int take, CancellationToken cancellationToken = default)
    {
        int boundedTake = Math.Clamp(take, 1, 100);
        var entries = new List<SystemSettingsBackupItem>(boundedTake);
        await foreach (ObjectListing obj in _storage.ListKeysAsync(BackupPrefix, cancellationToken))
        {
            entries.Add(new SystemSettingsBackupItem(obj.Key, obj.LastModifiedUtc, obj.ByteSize));
        }

        return entries
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(boundedTake)
            .ToList();
    }

    public async Task<bool> RestoreBackupAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(BackupPrefix, StringComparison.Ordinal))
            return false;

        await using Stream stream = await _storage.GetAsync(key, cancellationToken);
        SystemSettingsBackupPayload? payload = await JsonSerializer.DeserializeAsync<SystemSettingsBackupPayload>(stream, JsonOptions, cancellationToken);
        if (payload is null || payload.Items.Count == 0)
            return false;

        Dictionary<string, string> incoming = payload.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.Ordinal);

        foreach ((string itemKey, string itemValue) in incoming)
        {
            await _appSettingsService.SetValueAsync(itemKey, itemValue, cancellationToken);
        }
        return true;
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

    private sealed record AppSettingSnapshotItem(string Key, string Value);

    private sealed record SystemSettingsBackupPayload(
        int Version,
        string TriggerSource,
        DateTime CreatedAtUtc,
        IReadOnlyList<AppSettingSnapshotItem> Items);
}
