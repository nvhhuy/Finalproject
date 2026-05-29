using Application.Contracts.Authorization;
using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Domains.Billing;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Billing;

public sealed record PlanDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimits Limits,
    bool IsActive,
    int SortOrder);

public sealed record SubscriptionSummaryDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string PlanCode,
    string PlanName,
    string Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    bool AllowsWriteAccess);

public sealed record CreatePlanCommand(
    string Code,
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimits Limits,
    int SortOrder) : IRequest<Result<PlanDto, Error>>, IRequireAdmin;

public sealed class CreatePlanCommandValidator : AbstractValidator<CreatePlanCommand>
{
    public CreatePlanCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PriceMonthlyCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrialDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreatePlanCommandHandler : IRequestHandler<CreatePlanCommand, Result<PlanDto, Error>>
{
    private readonly IPlanStore _plans;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CreatePlanCommandHandler(IPlanStore plans, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _plans = plans;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<PlanDto, Error>> Handle(CreatePlanCommand request, CancellationToken cancellationToken)
    {
        string code = Plan.NormalizeCode(request.Code);
        if (await _plans.CodeExistsAsync(code, cancellationToken))
            return new ValidationError(nameof(request.Code), "Plan code already exists.");

        Plan plan = Plan.Create(
            code,
            request.Name,
            request.Description,
            request.PriceMonthlyCents,
            request.Currency,
            request.TrialDays,
            request.Limits.ToJson(),
            request.SortOrder,
            _clock.Now);
        _plans.Add(plan);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    internal static PlanDto ToDto(Plan plan) =>
        new(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.PriceMonthlyCents,
            plan.Currency,
            plan.TrialDays,
            PlanLimits.Parse(plan.LimitsJson),
            plan.IsActive,
            plan.SortOrder);
}

public sealed record UpdatePlanCommand(
    Guid PlanId,
    string Name,
    string? Description,
    int PriceMonthlyCents,
    string Currency,
    int TrialDays,
    PlanLimits Limits,
    bool IsActive,
    int SortOrder) : IRequest<Result<PlanDto, Error>>, IRequireAdmin;

public sealed class UpdatePlanCommandHandler : IRequestHandler<UpdatePlanCommand, Result<PlanDto, Error>>
{
    private readonly IPlanStore _plans;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public UpdatePlanCommandHandler(IPlanStore plans, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _plans = plans;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<PlanDto, Error>> Handle(UpdatePlanCommand request, CancellationToken cancellationToken)
    {
        Plan? plan = await _plans.GetByIdForUpdateAsync(request.PlanId, cancellationToken);
        if (plan is null)
            return new NotFoundError("Plan not found.");

        plan.Update(
            request.Name,
            request.Description,
            request.PriceMonthlyCents,
            request.Currency,
            request.TrialDays,
            request.Limits.ToJson(),
            request.IsActive,
            request.SortOrder,
            _clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);
        return CreatePlanCommandHandler.ToDto(plan);
    }
}

public sealed record DeletePlanCommand(Guid PlanId) : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class DeletePlanCommandHandler : IRequestHandler<DeletePlanCommand, Result<bool, Error>>
{
    private readonly IPlanStore _plans;
    private readonly IUnitOfWork _uow;

    public DeletePlanCommandHandler(IPlanStore plans, IUnitOfWork uow)
    {
        _plans = plans;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(DeletePlanCommand request, CancellationToken cancellationToken)
    {
        Plan? plan = await _plans.GetByIdForUpdateAsync(request.PlanId, cancellationToken);
        if (plan is null)
            return new NotFoundError("Plan not found.");

        if (await _plans.HasSubscriptionsAsync(request.PlanId, cancellationToken))
            return new ValidationError(
                nameof(request.PlanId),
                "Cannot delete a plan that has active or historical subscriptions. Deactivate it instead.");

        _plans.Remove(plan);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record ListPlansQuery(bool ActiveOnly = true, bool PublicCatalog = false)
    : IRequest<Result<IReadOnlyList<PlanDto>, Error>>;

public sealed class ListPlansQueryHandler : IRequestHandler<ListPlansQuery, Result<IReadOnlyList<PlanDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public ListPlansQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PlanDto>, Error>> Handle(ListPlansQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Plan> q = _db.Plans.AsNoTracking();
        if (request.ActiveOnly)
            q = q.Where(p => p.IsActive);
        List<Plan> plans = await q
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
        return plans.Select(CreatePlanCommandHandler.ToDto).ToList();
    }
}

/// <summary>
/// Manual checkout (MVP): authenticated user purchases a plan and becomes owner of a new tenant.
/// Payment provider webhooks will call the same provisioning path in Phase 1B follow-up.
/// </summary>
public sealed record SubscribeToPlanCommand(
    Guid UserId,
    string PlanCode,
    string TenantSlug,
    string TenantDisplayName,
    string? PublicTagline,
    string? PublicDescription) : IRequest<Result<SubscribeToPlanResult, Error>>;

public sealed record SubscribeToPlanResult(
    Guid TenantId,
    string TenantSlug,
    Guid SubscriptionId,
    string SubscriptionStatus);

public sealed class SubscribeToPlanCommandValidator : AbstractValidator<SubscribeToPlanCommand>
{
    public SubscribeToPlanCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlanCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(63);
        RuleFor(x => x.TenantDisplayName).NotEmpty().MaximumLength(200);
    }
}

public sealed class SubscribeToPlanCommandHandler : IRequestHandler<SubscribeToPlanCommand, Result<SubscribeToPlanResult, Error>>
{
    private readonly IPlanStore _plans;
    private readonly ITenantStore _tenantStore;
    private readonly IBillingInvoiceIssuer _invoices;
    private readonly IApplicationReadDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public SubscribeToPlanCommandHandler(
        IPlanStore plans,
        ITenantStore tenantStore,
        IBillingInvoiceIssuer invoices,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _plans = plans;
        _tenantStore = tenantStore;
        _invoices = invoices;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<SubscribeToPlanResult, Error>> Handle(SubscribeToPlanCommand request, CancellationToken cancellationToken)
    {
        string planCode = Plan.NormalizeCode(request.PlanCode);
        Plan? plan = await _plans.GetByCodeAsync(planCode, activeOnly: true, cancellationToken);
        if (plan is null)
            return new NotFoundError("Plan not found or inactive.");

        string slug = Tenant.NormalizeSlug(request.TenantSlug);
        if (await _tenantStore.SlugExistsAsync(slug, cancellationToken))
            return new ValidationError(nameof(request.TenantSlug), "Tenant slug is already in use.");

        UserId ownerId = UserId.Create(request.UserId);
        bool ownerExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == ownerId, cancellationToken);
        if (!ownerExists)
            return new NotFoundError("User not found.");

        Tenant tenant = Tenant.Create(slug, request.TenantDisplayName, ownerId, _clock.Now);
        tenant.UpdateBranding(request.PublicTagline, request.PublicDescription, _clock.Now);
        _tenantStore.Add(tenant);

        Subscription subscription = plan.TrialDays > 0
            ? Subscription.CreateTrialing(tenant.Id, plan.Id, plan.TrialDays, _clock.Now)
            : Subscription.CreateActive(tenant.Id, plan.Id, _clock.Now);
        _plans.Add(subscription);

        await _uow.SaveChangesAsync(cancellationToken);

        await _invoices.IssueSubscriptionInvoiceAsync(
            tenant.Id.Value,
            subscription.Id,
            plan.Id,
            "manual",
            cancellationToken);

        return new SubscribeToPlanResult(
            tenant.Id.Value,
            tenant.Slug,
            subscription.Id,
            subscription.Status.ToString());
    }
}

public sealed record GetTenantSubscriptionQuery(Guid TenantId) : IRequest<Result<SubscriptionSummaryDto, Error>>;

public sealed class GetTenantSubscriptionQueryHandler : IRequestHandler<GetTenantSubscriptionQuery, Result<SubscriptionSummaryDto, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public GetTenantSubscriptionQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<SubscriptionSummaryDto, Error>> Handle(
        GetTenantSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        SubscriptionSummaryDto? row = await (
            from s in _db.Subscriptions.AsNoTracking()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id
            where s.TenantId == TenantId.Create(request.TenantId)
            select new SubscriptionSummaryDto(
                s.Id,
                s.TenantId.Value,
                s.PlanId,
                p.Code,
                p.Name,
                s.Status.ToString(),
                s.CurrentPeriodStartUtc,
                s.CurrentPeriodEndUtc,
                s.Status == SubscriptionStatus.Trialing || s.Status == SubscriptionStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new NotFoundError("No subscription for this tenant.");

        return row;
    }
}
