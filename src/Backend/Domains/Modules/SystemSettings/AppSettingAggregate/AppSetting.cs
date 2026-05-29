namespace Domains.SystemSettings;

/// <summary>Simple key-value setting persisted for runtime feature toggles.</summary>
public sealed class AppSetting
{
    private AppSetting()
    {
    }

    private AppSetting(string key, string value)
    {
        Key = key;
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public DateTime UpdatedAtUtc { get; private set; }

    public static AppSetting Create(string key, string value)
    {
        return new AppSetting(key, value);
    }

    public void SetValue(string value)
    {
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}