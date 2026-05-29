using Domains.Common;
using Domains.Tenancy.ValueObjects;

namespace Domains.Billing;

public sealed class Subscription : AggregateRoot<Guid>
{
    private Subscription()
    {
    }

    private Subscription(
        Guid id,
        TenantId tenantId,
        Guid planId,
        SubscriptionStatus status,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime utcNow) : base(id)
    {
        TenantId = tenantId;
        PlanId = planId;
        Status = status;
        CurrentPeriodStartUtc = periodStartUtc;
        CurrentPeriodEndUtc = periodEndUtc;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public TenantId TenantId { get; private set; } = null!;
    public Guid PlanId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime CurrentPeriodStartUtc { get; private set; }
    public DateTime CurrentPeriodEndUtc { get; private set; }
    public DateTime? CanceledAtUtc { get; private set; }
    public DateTime? LastMonthlyCreditGrantUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public bool AllowsWriteAccess =>
        Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active;

    public static Subscription CreateTrialing(
        TenantId tenantId,
        Guid planId,
        int trialDays,
        DateTime utcNow)
    {
        if (trialDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(trialDays),
                "Trial days must be positive for trialing subscription.");
        var end = utcNow.AddDays(trialDays);
        return new Subscription(Guid.NewGuid(), tenantId, planId, SubscriptionStatus.Trialing, utcNow, end, utcNow);
    }

    public static Subscription CreateActive(
        TenantId tenantId,
        Guid planId,
        DateTime utcNow)
    {
        var end = utcNow.AddMonths(1);
        return new Subscription(Guid.NewGuid(), tenantId, planId, SubscriptionStatus.Active, utcNow, end, utcNow);
    }

    public void Activate(DateTime utcNow)
    {
        Status = SubscriptionStatus.Active;
        CurrentPeriodStartUtc = utcNow;
        CurrentPeriodEndUtc = utcNow.AddMonths(1);
        CanceledAtUtc = null;
        UpdatedAtUtc = utcNow;
    }

    public void MarkPastDue(DateTime utcNow)
    {
        Status = SubscriptionStatus.PastDue;
        UpdatedAtUtc = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void ChangePlan(Guid newPlanId, DateTime utcNow)
    {
        if (Status == SubscriptionStatus.Canceled)
            throw new InvalidOperationException("Cannot change plan on a canceled subscription.");
        if (newPlanId == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(newPlanId));

        PlanId = newPlanId;
        UpdatedAtUtc = utcNow;
    }

    public bool NeedsMonthlyCreditGrant(DateTime utcNow)
    {
        return LastMonthlyCreditGrantUtc is null
               || LastMonthlyCreditGrantUtc.Value.Year != utcNow.Year
               || LastMonthlyCreditGrantUtc.Value.Month != utcNow.Month;
    }

    public void RecordMonthlyCreditGrant(DateTime utcNow)
    {
        LastMonthlyCreditGrantUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }
}