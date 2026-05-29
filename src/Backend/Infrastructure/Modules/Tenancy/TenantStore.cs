using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Tenancy;

public sealed class TenantStore : ITenantStore
{
    private readonly ApplicationDbContext _db;

    public TenantStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> SlugExistsAsync(string normalizedSlug, CancellationToken cancellationToken = default)
    {
        return _db.Tenants.AsNoTracking().AnyAsync(t => t.Slug == normalizedSlug, cancellationToken);
    }

    public Task<Tenant?> GetByIdForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return _db.Tenants
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
    }

    public void Add(Tenant tenant)
    {
        _db.Tenants.Add(tenant);
    }
}