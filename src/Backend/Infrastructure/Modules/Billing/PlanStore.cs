using Application.Contracts.Billing;
using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class PlanStore : IPlanStore
{
    private readonly ApplicationDbContext _db;

    public PlanStore(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return _db.Plans.AsNoTracking().AnyAsync(p => p.Code == normalizedCode, cancellationToken);
    }

    public Task<Plan?> GetByCodeAsync(string normalizedCode, bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Plans.Where(p => p.Code == normalizedCode);
        if (activeOnly)
            q = q.Where(p => p.IsActive);
        return q.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Plan?> GetByIdForUpdateAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return _db.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
    }

    public Task<bool> HasSubscriptionsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        return _db.Subscriptions.AsNoTracking().AnyAsync(s => s.PlanId == planId, cancellationToken);
    }

    public void Remove(Plan plan)
    {
        _db.Plans.Remove(plan);
    }

    public void Add(Plan plan)
    {
        _db.Plans.Add(plan);
    }

    public void Add(Subscription subscription)
    {
        _db.Subscriptions.Add(subscription);
    }

    public Task<bool> TenantHasSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return _db.Subscriptions.AsNoTracking()
            .AnyAsync(s => s.TenantId == TenantId.Create(tenantId), cancellationToken);
    }
}