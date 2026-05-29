using System.Text;
using System.Text.Json;
using Infrastructure.Settings;

namespace Infrastructure.Tests.Settings;

public sealed class SystemSettingsBackupParseTests
{
    [Fact]
    public void TryParseSettingsBackup_parses_platform_snapshot_format()
    {
        const string json = """
                            {
                              "version": 1,
                              "triggerSource": "legacy",
                              "createdAtUtc": "2026-01-01T00:00:00Z",
                              "items": [
                                { "key": "feature.admin.database_backup.enabled", "value": "true" },
                                { "key": "feature.admin.database_backup.retention_days", "value": "14" }
                              ]
                            }
                            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parsed = SystemSettingsBackupService.TryParseSettingsBackup(stream, out var settings);

        Assert.True(parsed);
        Assert.NotNull(settings);
        Assert.Equal("true", settings["feature.admin.database_backup.enabled"]);
        Assert.Equal("14", settings["feature.admin.database_backup.retention_days"]);
    }

    [Fact]
    public void TryParseSettingsBackup_parses_legacy_key_value_array()
    {
        const string json = """
                            [
                              { "key": "alpha", "value": "1" },
                              { "key": "beta", "value": "2" }
                            ]
                            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parsed = SystemSettingsBackupService.TryParseSettingsBackup(stream, out var settings);

        Assert.True(parsed);
        Assert.NotNull(settings);
        Assert.Equal(2, settings.Count);
        Assert.Equal("1", settings["alpha"]);
        Assert.Equal("2", settings["beta"]);
    }

    [Fact]
    public void TryParseSettingsBackup_parses_flat_object_map()
    {
        var payload = new Dictionary<string, string>
        {
            ["feature.x"] = "on",
            ["feature.y"] = "off"
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);

        using var stream = new MemoryStream(json);
        var parsed = SystemSettingsBackupService.TryParseSettingsBackup(stream, out var settings);

        Assert.True(parsed);
        Assert.NotNull(settings);
        Assert.Equal("on", settings["feature.x"]);
        Assert.Equal("off", settings["feature.y"]);
    }
}