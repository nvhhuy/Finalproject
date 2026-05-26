using Application.Contracts.Settings;
using Application.Contracts.Transactions;
using Domains.SystemSettings;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Settings;

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly ApplicationDbContext _write;
    private readonly ApplicationReadDbContext _read;
    private readonly IUnitOfWork _uow;

    public AppSettingsService(ApplicationDbContext write, ApplicationReadDbContext read, IUnitOfWork uow)
    {
        _write = write;
        _read = read;
        _uow = uow;
    }

    public async Task<bool> GetBooleanAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        string? value = await GetValueAsync(key, cancellationToken);
        return bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        AppSetting? setting = await _read.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        return setting?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        AppSetting? existing = await _write.AppSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            _write.AppSettings.Add(AppSetting.Create(key, value));
        }
        else
        {
            existing.SetValue(value);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
