using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;

namespace Application.Contracts.Tenancy;

public interface ITenantDomainStore
{
    void Add(TenantDomain domain);

    Task<TenantDomain?> GetForUpdateAsync(Guid domainId, TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> HostnameExistsAsync(string hostname, CancellationToken cancellationToken = default);
}