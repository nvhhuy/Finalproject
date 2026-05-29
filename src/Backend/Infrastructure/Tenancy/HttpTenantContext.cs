using Application.Contracts.Tenancy;

namespace Infrastructure.Tenancy;

public sealed class HttpTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public bool BypassTenantIsolation { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id cannot be empty.", nameof(tenantId));
        TenantId = tenantId;
    }

    public void EnableBypass() => BypassTenantIsolation = true;
}
