using Application.Contracts.Authorization;
using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Tenancy;

public sealed record TenantSummaryDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string Status,
    Guid OwnerUserId,
    DateTime CreatedAtUtc,
    string? SubscriptionPlanCode = null,
    string? SubscriptionStatus = null);

public sealed record TenantPublicDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? PublicTagline,
    string? PublicDescription);

public sealed record CreateTenantCommand(
    string Slug,
    string DisplayName,
    Guid OwnerUserId,
    string? PublicTagline,
    string? PublicDescription) : IRequest<Result<TenantSummaryDto, Error>>, IRequireAdmin;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(63);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.PublicTagline).MaximumLength(300);
        RuleFor(x => x.PublicDescription).MaximumLength(4000);
    }
}

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Result<TenantSummaryDto, Error>>
{
    private readonly ITenantStore _tenantStore;
    private readonly IApplicationReadDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public CreateTenantCommandHandler(
        ITenantStore tenantStore,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _tenantStore = tenantStore;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<TenantSummaryDto, Error>> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        string normalizedSlug = Tenant.NormalizeSlug(request.Slug);
        if (await _tenantStore.SlugExistsAsync(normalizedSlug, cancellationToken))
            return new ValidationError(nameof(request.Slug), "Slug is already in use.");

        UserId ownerId = UserId.Create(request.OwnerUserId);
        bool ownerExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == ownerId, cancellationToken);
        if (!ownerExists)
            return new NotFoundError("Owner user was not found.");

        Tenant tenant = Tenant.Create(normalizedSlug, request.DisplayName, ownerId, _clock.Now);
        tenant.UpdateBranding(request.PublicTagline, request.PublicDescription, _clock.Now);
        _tenantStore.Add(tenant);
        await _uow.SaveChangesAsync(cancellationToken);

        return ToSummary(tenant);
    }

    internal static TenantSummaryDto ToSummary(Tenant tenant) =>
        new(
            tenant.Id.Value,
            tenant.Slug,
            tenant.DisplayName,
            tenant.Status.ToString(),
            tenant.OwnerUserId.Value,
            tenant.CreatedAtUtc);
}

public sealed record ListTenantsQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<TenantSummaryDto>, Error>>, IRequireAdmin;

public sealed class ListTenantsQueryHandler : IRequestHandler<ListTenantsQuery, Result<IReadOnlyList<TenantSummaryDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public ListTenantsQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TenantSummaryDto>, Error>> Handle(
        ListTenantsQuery request,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        int skip = (page - 1) * pageSize;

        var rows = await (
            from t in _db.Tenants.AsNoTracking()
            join s in _db.Subscriptions.AsNoTracking() on t.Id equals s.TenantId into subs
            from s in subs.DefaultIfEmpty()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id into plans
            from p in plans.DefaultIfEmpty()
            orderby t.CreatedAtUtc descending
            select new TenantSummaryDto(
                t.Id.Value,
                t.Slug,
                t.DisplayName,
                t.Status.ToString(),
                t.OwnerUserId.Value,
                t.CreatedAtUtc,
                p != null ? p.Code : null,
                s != null ? s.Status.ToString() : null))
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record GetTenantBySlugQuery(string Slug) : IRequest<Result<TenantPublicDto, Error>>;

public sealed class GetTenantBySlugQueryHandler : IRequestHandler<GetTenantBySlugQuery, Result<TenantPublicDto, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public GetTenantBySlugQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<TenantPublicDto, Error>> Handle(GetTenantBySlugQuery request, CancellationToken cancellationToken)
    {
        string slug;
        try
        {
            slug = Tenant.NormalizeSlug(request.Slug);
        }
        catch (ArgumentException ex)
        {
            return new ValidationError(nameof(request.Slug), ex.Message);
        }

        TenantPublicDto? tenant = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug && t.Status == TenantStatus.Active)
            .Select(t => new TenantPublicDto(
                t.Id.Value,
                t.Slug,
                t.DisplayName,
                t.PublicTagline,
                t.PublicDescription))
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
            return new NotFoundError("Tenant was not found.");

        return tenant;
    }
}

public sealed record GetMyTenantsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<TenantSummaryDto>, Error>>;

public sealed class GetMyTenantsQueryHandler : IRequestHandler<GetMyTenantsQuery, Result<IReadOnlyList<TenantSummaryDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public GetMyTenantsQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<TenantSummaryDto>, Error>> Handle(
        GetMyTenantsQuery request,
        CancellationToken cancellationToken)
    {
        UserId userId = UserId.Create(request.UserId);
        List<TenantId> memberTenantIds = await _db.TenantMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        var rows = await (
            from t in _db.Tenants.AsNoTracking()
            where memberTenantIds.Contains(t.Id)
            join s in _db.Subscriptions.AsNoTracking() on t.Id equals s.TenantId into subs
            from s in subs.DefaultIfEmpty()
            join p in _db.Plans.AsNoTracking() on s.PlanId equals p.Id into plans
            from p in plans.DefaultIfEmpty()
            orderby t.DisplayName
            select new TenantSummaryDto(
                t.Id.Value,
                t.Slug,
                t.DisplayName,
                t.Status.ToString(),
                t.OwnerUserId.Value,
                t.CreatedAtUtc,
                p != null ? p.Code : null,
                s != null ? s.Status.ToString() : null))
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record InviteTenantMemberCommand(
    Guid TenantId,
    Guid InviterUserId,
    string Email,
    string Role = TenantMemberRoles.Member) : IRequest<Result<TenantMemberSummaryDto, Error>>;

public sealed record TenantMemberSummaryDto(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    string Role,
    DateTime JoinedAtUtc);

public sealed class InviteTenantMemberCommandValidator : AbstractValidator<InviteTenantMemberCommand>
{
    public InviteTenantMemberCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.InviterUserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).NotEmpty();
    }
}

public sealed class InviteTenantMemberCommandHandler : IRequestHandler<InviteTenantMemberCommand, Result<TenantMemberSummaryDto, Error>>
{
    private readonly ITenantStore _tenantStore;
    private readonly ITenantMembershipService _membership;
    private readonly IEntitlementService _entitlements;
    private readonly IApplicationReadDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public InviteTenantMemberCommandHandler(
        ITenantStore tenantStore,
        ITenantMembershipService membership,
        IEntitlementService entitlements,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _tenantStore = tenantStore;
        _membership = membership;
        _entitlements = entitlements;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<TenantMemberSummaryDto, Error>> Handle(
        InviteTenantMemberCommand request,
        CancellationToken cancellationToken)
    {
        TenantId tenantId = TenantId.Create(request.TenantId);
        if (!await _membership.IsMemberAsync(request.TenantId, request.InviterUserId, cancellationToken))
            return new ForbiddenError("You are not a member of this tenant.");

        EntitlementCheckResult entitlement = await _entitlements.CheckMemberInviteAsync(request.TenantId, cancellationToken);
        if (!entitlement.Allowed)
            return new ValidationError("subscription", entitlement.DenyReason ?? "Cannot invite members.");

        Tenant? tenant = await _tenantStore.GetByIdForUpdateAsync(tenantId, cancellationToken);
        if (tenant is null)
            return new NotFoundError("Tenant was not found.");

        string email = request.Email.Trim();
        UserId? inviteeId = await _db.Users.AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (inviteeId is null)
            return new NotFoundError("User with this email was not found. They must register first.");

        if (!TenantMemberRoles.IsValid(request.Role))
            return new ValidationError(nameof(request.Role), "Invalid role.");

        try
        {
            TenantMember member = tenant.AddMember(inviteeId, request.Role, _clock.Now);
            await _uow.SaveChangesAsync(cancellationToken);
            return new TenantMemberSummaryDto(
                member.Id,
                tenant.Id.Value,
                member.UserId.Value,
                member.Role,
                member.JoinedAtUtc);
        }
        catch (InvalidOperationException ex)
        {
            return new ValidationError("email", ex.Message);
        }
    }
}
