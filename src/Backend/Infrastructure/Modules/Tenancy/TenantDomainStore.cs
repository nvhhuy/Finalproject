using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Tenancy;

public sealed class TenantDomainStore : ITenantDomainStore
{
    private readonly ApplicationDbContext _db;

    public TenantDomainStore(ApplicationDbContext db) => _db = db;

    public void Add(TenantDomain domain) => _db.TenantDomains.Add(domain);

    public Task<TenantDomain?> GetForUpdateAsync(
        Guid domainId,
        TenantId tenantId,
        CancellationToken cancellationToken = default) =>
        _db.TenantDomains
            .FirstOrDefaultAsync(d => d.Id == domainId && d.TenantId == tenantId, cancellationToken);

    public Task<bool> HostnameExistsAsync(string hostname, CancellationToken cancellationToken = default) =>
        _db.TenantDomains.AsNoTracking()
            .AnyAsync(d => d.Hostname == hostname && d.Status != TenantDomainStatus.Disabled, cancellationToken);
}
