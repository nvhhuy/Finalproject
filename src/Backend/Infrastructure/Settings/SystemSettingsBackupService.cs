using System.Text.Json;
using Application.Contracts.Settings;
using Application.Contracts.Storage;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Settings;

public sealed class SystemSettingsBackupService : ISystemSettingsBackupService
{
    private const string BackupPrefix = "system-backups/settings/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationReadDbContext _read;
    private readonly IObjectStorage _storage;
    private readonly IAppSettingsService _appSettingsService;
    private readonly long _maxImportBytes;

    public SystemSettingsBackupService(
        ApplicationReadDbContext read,
        IObjectStorage storage,
        IAppSettingsService appSettingsService,
        IOptions<DatabaseBackupOptions> options)
    {
        _read = read;
        _storage = storage;
        _appSettingsService = appSettingsService;
        _maxImportBytes = Math.Max(1, options.Value.MaxSettingsImportBytes);
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
        if (!TryParseSettingsBackup(stream, out Dictionary<string, string>? settings) || settings is null)
            return false;

        await ApplySettingsAsync(settings, cancellationToken);
        return true;
    }

    public async Task<SystemBackupDownloadResult?> DownloadBackupAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(BackupPrefix, StringComparison.Ordinal))
            return null;

        try
        {
            await using Stream stream = await _storage.GetAsync(key, cancellationToken);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length == 0)
                return null;

            string fileName = ResolveDownloadFileName(key, "settings-backup.json");
            return new SystemBackupDownloadResult(buffer.ToArray(), "application/json", fileName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ImportSystemSettingsBackupResult?> ImportAndRestoreBackupAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (content is not { CanRead: true })
            return null;

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0 || buffer.Length > _maxImportBytes)
            return null;

        buffer.Position = 0;
        if (!TryParseSettingsBackup(buffer, out Dictionary<string, string>? settings) || settings is null || settings.Count == 0)
            return null;

        await ApplySettingsAsync(settings, cancellationToken);

        DateTime now = DateTime.UtcNow;
        string backupKey = $"{BackupPrefix}imported/{now:yyyyMMddHHmmssfff}-settings.json";
        buffer.Position = 0;
        await _storage.PutAsync(backupKey, buffer, "application/json", cancellationToken);
        return new ImportSystemSettingsBackupResult(backupKey, now, buffer.Length, settings.Count);
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

    private async Task ApplySettingsAsync(Dictionary<string, string> settings, CancellationToken cancellationToken)
    {
        foreach ((string itemKey, string itemValue) in settings)
        {
            await _appSettingsService.SetValueAsync(itemKey, itemValue, cancellationToken);
        }
    }

    internal static bool TryParseSettingsBackup(Stream stream, out Dictionary<string, string>? settings)
    {
        settings = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("items", out JsonElement itemsElement)
                && itemsElement.ValueKind == JsonValueKind.Array)
            {
                settings = ParseItemArray(itemsElement);
                return settings.Count > 0;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                settings = ParseItemArray(root);
                return settings.Count > 0;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                settings = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.NameEquals("version")
                        || property.NameEquals("triggerSource")
                        || property.NameEquals("createdAtUtc")
                        || property.NameEquals("items"))
                        continue;

                    settings[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.True => bool.TrueString,
                        JsonValueKind.False => bool.FalseString,
                        JsonValueKind.Number => property.Value.GetRawText(),
                        _ => property.Value.GetRawText(),
                    };
                }

                return settings.Count > 0;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string ResolveDownloadFileName(string key, string fallback)
    {
        string segment = key.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
        return string.IsNullOrWhiteSpace(segment) ? fallback : segment;
    }

    private static Dictionary<string, string> ParseItemArray(JsonElement array)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            if (!element.TryGetProperty("key", out JsonElement keyElement))
                continue;

            string? key = keyElement.GetString();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            string value = element.TryGetProperty("value", out JsonElement valueElement)
                ? valueElement.ValueKind switch
                {
                    JsonValueKind.String => valueElement.GetString() ?? string.Empty,
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    JsonValueKind.Number => valueElement.GetRawText(),
                    _ => valueElement.GetRawText(),
                }
                : string.Empty;

            settings[key] = value;
        }

        return settings;
    }

    private sealed record AppSettingSnapshotItem(string Key, string Value);

    private sealed record SystemSettingsBackupPayload(
        int Version,
        string TriggerSource,
        DateTime CreatedAtUtc,
        IReadOnlyList<AppSettingSnapshotItem> Items);
}
