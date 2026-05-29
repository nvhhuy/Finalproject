using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;

namespace Application.Contracts.Tenancy;

public interface ITenantSsoSettingsStore
{
    void Add(TenantSsoSettings settings);

    Task<TenantSsoSettings?> GetForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default);
}
