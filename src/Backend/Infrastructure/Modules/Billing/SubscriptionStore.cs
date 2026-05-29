using Application.Contracts.Billing;
using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class SubscriptionStore : ISubscriptionStore
{
    private readonly ApplicationDbContext _db;

    public SubscriptionStore(ApplicationDbContext db) => _db = db;

    public Task<Subscription?> GetByTenantForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default) =>
        _db.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListExpiredActiveOrTrialingAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        await _db.Subscriptions
            .Where(s =>
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing) &&
                s.CurrentPeriodEndUtc < utcNow)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListPastDueAsync(CancellationToken cancellationToken = default) =>
        await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.PastDue)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> ListActiveOrTrialingForUpdateAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
            .ToListAsync(cancellationToken);
}
