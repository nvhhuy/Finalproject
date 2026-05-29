namespace Application.Contracts.Billing;

public sealed record TenantUsageSnapshot(
    int MemberCount,
    long StorageBytes,
    int OpenSupportTicketCount);

public interface IEntitlementService
{
    Task<bool> CanUseModuleAsync(Guid tenantId, string moduleCode, CancellationToken cancellationToken = default);

    Task<bool> AllowsWriteAccessAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<PlanLimits?> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<EntitlementCheckResult> CheckMemberInviteAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<EntitlementCheckResult> CheckOpenSupportTicketAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record EntitlementCheckResult(bool Allowed, string? DenyReason = null)
{
    public static EntitlementCheckResult Ok() => new(true);
    public static EntitlementCheckResult Deny(string reason) => new(false, reason);
}
