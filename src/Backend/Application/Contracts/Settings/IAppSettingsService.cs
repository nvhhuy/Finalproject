namespace Application.Contracts.Settings;

public interface IAppSettingsService
{
    Task<bool> GetBooleanAsync(string key, bool defaultValue, CancellationToken cancellationToken = default);
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}
