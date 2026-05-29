using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;

namespace Application.Contracts.Tenancy;

public interface ITenantStore
{
    Task<bool> SlugExistsAsync(string normalizedSlug, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    void Add(Tenant tenant);
}
