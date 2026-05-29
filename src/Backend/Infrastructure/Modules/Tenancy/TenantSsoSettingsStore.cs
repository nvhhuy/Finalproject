using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Tenancy;

public sealed class TenantSsoSettingsStore : ITenantSsoSettingsStore
{
    private readonly ApplicationDbContext _db;

    public TenantSsoSettingsStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public void Add(TenantSsoSettings settings)
    {
        _db.TenantSsoSettings.Add(settings);
    }

    public Task<TenantSsoSettings?> GetForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return _db.TenantSsoSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
    }
}