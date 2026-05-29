using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Domains.Billing;
using Domains.Support;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class EntitlementService : IEntitlementService
{
    private readonly ApplicationReadDbContext _db;

    public EntitlementService(ApplicationReadDbContext db) => _db = db;

    public async Task<bool> CanUseModuleAsync(Guid tenantId, string moduleCode, CancellationToken cancellationToken = default)
    {
        if (tenantId == Domains.Tenancy.SystemTenants.DefaultTenantId)
            return true;

        PlanLimits? limits = await GetLimitsAsync(tenantId, cancellationToken);
        if (limits is null)
            return false;
        return limits.HasModule(moduleCode);
    }

    public async Task<bool> AllowsWriteAccessAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        SubscriptionStatus? status = await GetSubscriptionStatusAsync(tenantId, cancellationToken);
        if (status is null)
            return tenantId == Domains.Tenancy.SystemTenants.DefaultTenantId;
        return status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;
    }

    public async Task<PlanLimits?> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        string? limitsJson = await (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id
            where s.TenantId == TenantId.Create(tenantId)
            select p.LimitsJson)
            .FirstOrDefaultAsync(cancellationToken);

        return limitsJson is null ? null : PlanLimits.Parse(limitsJson);
    }

    public async Task<TenantUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        TenantId tenantKey = TenantId.Create(tenantId);
        int members = await _db.TenantMembers.AsNoTracking().CountAsync(m => m.TenantId == tenantKey, cancellationToken);
        long storage = await _db.MediaAssets.AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .SumAsync(m => (long?)m.ByteSize, cancellationToken) ?? 0;
        int openTickets = await _db.SupportTickets.AsNoTracking()
            .CountAsync(
                t => t.TenantId == tenantId && t.Status != SupportTicketStatus.Closed,
                cancellationToken);
        return new TenantUsageSnapshot(members, storage, openTickets);
    }

    public async Task<EntitlementCheckResult> CheckMemberInviteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!await AllowsWriteAccessAsync(tenantId, cancellationToken))
            return EntitlementCheckResult.Deny("Subscription is read-only. Renew to invite members.");

        PlanLimits? limits = await GetLimitsAsync(tenantId, cancellationToken);
        if (limits is null)
            return EntitlementCheckResult.Ok();

        TenantUsageSnapshot usage = await GetUsageAsync(tenantId, cancellationToken);
        if (usage.MemberCount >= limits.MaxUsers)
            return EntitlementCheckResult.Deny($"Member limit reached ({limits.MaxUsers}). Upgrade your plan.");

        return EntitlementCheckResult.Ok();
    }

    public async Task<EntitlementCheckResult> CheckOpenSupportTicketAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!await AllowsWriteAccessAsync(tenantId, cancellationToken))
            return EntitlementCheckResult.Deny("Subscription is read-only. Renew to create tickets.");

        if (!await CanUseModuleAsync(tenantId, BillingModules.Support, cancellationToken))
            return EntitlementCheckResult.Deny("Support module is not included in your plan.");

        PlanLimits? limits = await GetLimitsAsync(tenantId, cancellationToken);
        if (limits is null)
            return EntitlementCheckResult.Ok();

        TenantUsageSnapshot usage = await GetUsageAsync(tenantId, cancellationToken);
        if (usage.OpenSupportTicketCount >= limits.MaxOpenSupportTickets)
            return EntitlementCheckResult.Deny($"Open ticket limit reached ({limits.MaxOpenSupportTickets}).");

        return EntitlementCheckResult.Ok();
    }

    private async Task<SubscriptionStatus?> GetSubscriptionStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        return await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.TenantId == TenantId.Create(tenantId))
            .Select(s => (SubscriptionStatus?)s.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
