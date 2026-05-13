namespace Domains.SystemSettings;

/// <summary>Simple key-value setting persisted for runtime feature toggles.</summary>
public sealed class AppSetting
{
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public DateTime UpdatedAtUtc { get; private set; }

    private AppSetting() { }

    private AppSetting(string key, string value)
    {
        Key = key;
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static AppSetting Create(string key, string value) => new(key, value);

    public void SetValue(string value)
    {
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
