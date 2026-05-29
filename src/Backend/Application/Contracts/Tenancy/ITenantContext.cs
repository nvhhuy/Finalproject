namespace Application.Contracts.Tenancy;

/// <summary>Request-scoped tenant resolution for multi-tenant isolation.</summary>
public interface ITenantContext
{
    /// <summary>Resolved tenant for the current request, if any.</summary>
    Guid? TenantId { get; }

    /// <summary>When true, EF global tenant filters are bypassed (platform administrator).</summary>
    bool BypassTenantIsolation { get; }

    void SetTenant(Guid tenantId);

    void EnableBypass();
}