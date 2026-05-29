using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Billing;

public sealed record CancelTenantSubscriptionCommand(Guid TenantId, Guid UserId)
    : IRequest<Result<SubscriptionSummaryDto, Error>>;

public sealed record ChangeTenantSubscriptionPlanCommand(Guid TenantId, Guid UserId, string PlanCode)
    : IRequest<Result<SubscriptionSummaryDto, Error>>;

public sealed record ProcessSubscriptionDunningCommand : IRequest<Result<int, Error>>;

public sealed class CancelTenantSubscriptionCommandValidator : AbstractValidator<CancelTenantSubscriptionCommand>
{
    public CancelTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class ChangeTenantSubscriptionPlanCommandValidator : AbstractValidator<ChangeTenantSubscriptionPlanCommand>
{
    public ChangeTenantSubscriptionPlanCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64);
    }
}

public sealed class CancelTenantSubscriptionCommandHandler
    : IRequestHandler<CancelTenantSubscriptionCommand, Result<SubscriptionSummaryDto, Error>>
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly IApplicationReadDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CancelTenantSubscriptionCommandHandler(
        ISubscriptionStore subscriptions,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<SubscriptionSummaryDto, Error>> Handle(
        CancelTenantSubscriptionCommand request,
        CancellationToken cancellationToken)
    {
        if (!await IsTenantOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can cancel the subscription.");

        TenantId tenantId = TenantId.Create(request.TenantId);
        Subscription? subscription = await _subscriptions.GetByTenantForUpdateAsync(tenantId, cancellationToken);
        if (subscription is null)
            return new NotFoundError("No subscription for this tenant.");

        subscription.Cancel(_clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);
        return await LoadSummaryAsync(subscription, cancellationToken);
    }

    private async Task<bool> IsTenantOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        UserId ownerId = UserId.Create(userId);
        return await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == TenantId.Create(tenantId) && t.OwnerUserId == ownerId, cancellationToken);
    }

    private async Task<Result<SubscriptionSummaryDto, Error>> LoadSummaryAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        SubscriptionSummaryDto? row = await (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id
            where s.Id == subscription.Id
            select new SubscriptionSummaryDto(
                s.Id,
                s.TenantId.Value,
                s.PlanId,
                p.Code,
                p.Name,
                s.Status.ToString(),
                s.CurrentPeriodStartUtc,
                s.CurrentPeriodEndUtc,
                s.AllowsWriteAccess))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new NotFoundError("Subscription summary was not found.");
        return row;
    }
}

public sealed class ChangeTenantSubscriptionPlanCommandHandler
    : IRequestHandler<ChangeTenantSubscriptionPlanCommand, Result<SubscriptionSummaryDto, Error>>
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly IPlanStore _plans;
    private readonly IBillingInvoiceIssuer _invoices;
    private readonly IApplicationReadDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public ChangeTenantSubscriptionPlanCommandHandler(
        ISubscriptionStore subscriptions,
        IPlanStore plans,
        IBillingInvoiceIssuer invoices,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _plans = plans;
        _invoices = invoices;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<SubscriptionSummaryDto, Error>> Handle(
        ChangeTenantSubscriptionPlanCommand request,
        CancellationToken cancellationToken)
    {
        if (!await IsTenantOwnerAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("Only the tenant owner can change the subscription plan.");

        string planCode = Plan.NormalizeCode(request.PlanCode);
        Plan? plan = await _plans.GetByCodeAsync(planCode, activeOnly: true, cancellationToken);
        if (plan is null)
            return new NotFoundError("Plan not found or inactive.");

        TenantId tenantId = TenantId.Create(request.TenantId);
        Subscription? subscription = await _subscriptions.GetByTenantForUpdateAsync(tenantId, cancellationToken);
        if (subscription is null)
            return new NotFoundError("No subscription for this tenant.");

        if (subscription.Status == SubscriptionStatus.Canceled)
            return new ValidationError(nameof(request.PlanCode), "Cannot change plan on a canceled subscription.");

        subscription.ChangePlan(plan.Id, _clock.Now);
        if (subscription.Status == SubscriptionStatus.PastDue)
            subscription.Activate(_clock.Now);

        await _uow.SaveChangesAsync(cancellationToken);

        await _invoices.IssuePlanChangeInvoiceAsync(
            request.TenantId,
            subscription.Id,
            plan.Id,
            "manual",
            cancellationToken);

        return await LoadSummaryAsync(subscription, cancellationToken);
    }

    private async Task<bool> IsTenantOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        UserId ownerId = UserId.Create(userId);
        return await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == TenantId.Create(tenantId) && t.OwnerUserId == ownerId, cancellationToken);
    }

    private async Task<Result<SubscriptionSummaryDto, Error>> LoadSummaryAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        SubscriptionSummaryDto? row = await (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id
            where s.Id == subscription.Id
            select new SubscriptionSummaryDto(
                s.Id,
                s.TenantId.Value,
                s.PlanId,
                p.Code,
                p.Name,
                s.Status.ToString(),
                s.CurrentPeriodStartUtc,
                s.CurrentPeriodEndUtc,
                s.AllowsWriteAccess))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new NotFoundError("Subscription summary was not found.");
        return row;
    }
}

public sealed class ProcessSubscriptionDunningCommandHandler : IRequestHandler<ProcessSubscriptionDunningCommand, Result<int, Error>>
{
    private readonly ISubscriptionStore _subscriptions;
    private readonly IApplicationReadDbContext _db;
    private readonly IBillingDunningNotifier _dunning;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public ProcessSubscriptionDunningCommandHandler(
        ISubscriptionStore subscriptions,
        IApplicationReadDbContext db,
        IBillingDunningNotifier dunning,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _subscriptions = subscriptions;
        _db = db;
        _dunning = dunning;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<int, Error>> Handle(ProcessSubscriptionDunningCommand request, CancellationToken cancellationToken)
    {
        int notified = 0;
        DateTime now = _clock.Now;

        IReadOnlyList<Subscription> expired = await _subscriptions.ListExpiredActiveOrTrialingAsync(now, cancellationToken);
        foreach (Subscription subscription in expired)
        {
            subscription.MarkPastDue(now);
            notified += await TryNotifyOwnerAsync(subscription, cancellationToken) ? 1 : 0;
        }

        IReadOnlyList<Subscription> pastDue = await _subscriptions.ListPastDueAsync(cancellationToken);
        foreach (Subscription subscription in pastDue)
        {
            if (expired.Any(e => e.Id == subscription.Id))
                continue;
            notified += await TryNotifyOwnerAsync(subscription, cancellationToken) ? 1 : 0;
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return notified;
    }

    private async Task<bool> TryNotifyOwnerAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var row = await (
            from t in _db.Tenants.AsNoTracking()
            join u in _db.Users.AsNoTracking() on t.OwnerUserId equals u.Id
            join p in _db.Plans.AsNoTracking() on subscription.PlanId equals p.Id
            where t.Id == subscription.TenantId
            select new
            {
                t.DisplayName,
                Email = u.Email,
                PlanName = p.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null || string.IsNullOrWhiteSpace(row.Email))
            return false;

        await _dunning.NotifyPastDueAsync(
            subscription.TenantId.Value,
            subscription.Id,
            row.DisplayName,
            row.Email,
            row.PlanName,
            subscription.CurrentPeriodEndUtc,
            cancellationToken);
        return true;
    }
}
