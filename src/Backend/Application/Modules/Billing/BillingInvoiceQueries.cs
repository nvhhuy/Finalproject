using Application.Contracts.Authorization;
using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Tenancy;
using Domains.Tenancy.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Billing;

public sealed record BillingInvoiceListItemDto(
    Guid Id,
    Guid? TenantId,
    Guid? UserId,
    string InvoiceNumber,
    string Description,
    int AmountCents,
    long? AmountCredits,
    string Currency,
    string Status,
    string Provider,
    DateTime IssuedAtUtc);

public sealed record ListTenantBillingInvoicesQuery(Guid TenantId, Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>;

public sealed class ListTenantBillingInvoicesQueryHandler
    : IRequestHandler<ListTenantBillingInvoicesQuery, Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;
    private readonly ITenantMembershipService _membership;

    public ListTenantBillingInvoicesQueryHandler(IApplicationReadDbContext db, ITenantMembershipService membership)
    {
        _db = db;
        _membership = membership;
    }

    public async Task<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>> Handle(
        ListTenantBillingInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsMemberAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("You are not a member of this tenant.");

        TenantId tenantKey = TenantId.Create(request.TenantId);
        int skip = Math.Max(0, request.Page - 1) * Math.Clamp(request.PageSize, 1, 100);
        List<BillingInvoiceListItemDto> rows = await _db.BillingInvoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantKey)
            .OrderByDescending(i => i.IssuedAtUtc)
            .Skip(skip)
            .Take(Math.Clamp(request.PageSize, 1, 100))
            .Select(i => new BillingInvoiceListItemDto(
                i.Id,
                i.TenantId!.Value,
                i.UserId,
                i.InvoiceNumber,
                i.Description,
                i.AmountCents,
                i.AmountCredits,
                i.Currency,
                i.Status.ToString(),
                i.Provider,
                i.IssuedAtUtc))
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record ListMyWalletInvoicesQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>;

public sealed class ListMyWalletInvoicesQueryHandler : IRequestHandler<ListMyWalletInvoicesQuery, Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public ListMyWalletInvoicesQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>> Handle(
        ListMyWalletInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        int skip = Math.Max(0, request.Page - 1) * Math.Clamp(request.PageSize, 1, 100);
        List<BillingInvoiceListItemDto> rows = await _db.BillingInvoices
            .AsNoTracking()
            .Where(i => i.UserId == request.UserId)
            .OrderByDescending(i => i.IssuedAtUtc)
            .Skip(skip)
            .Take(Math.Clamp(request.PageSize, 1, 100))
            .Select(i => new BillingInvoiceListItemDto(
                i.Id,
                i.TenantId == null ? null : i.TenantId.Value,
                i.UserId,
                i.InvoiceNumber,
                i.Description,
                i.AmountCents,
                i.AmountCredits,
                i.Currency,
                i.Status.ToString(),
                i.Provider,
                i.IssuedAtUtc))
            .ToListAsync(cancellationToken);

        return rows;
    }
}

public sealed record ListPlatformBillingInvoicesQuery(int Page = 1, int PageSize = 50)
    : IRequest<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>, IRequireAdmin;

public sealed class ListPlatformBillingInvoicesQueryHandler
    : IRequestHandler<ListPlatformBillingInvoicesQuery, Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>>
{
    private readonly IApplicationReadDbContext _db;

    public ListPlatformBillingInvoicesQueryHandler(IApplicationReadDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<BillingInvoiceListItemDto>, Error>> Handle(
        ListPlatformBillingInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        int skip = Math.Max(0, request.Page - 1) * Math.Clamp(request.PageSize, 1, 100);
        List<BillingInvoiceListItemDto> rows = await _db.BillingInvoices
            .AsNoTracking()
            .OrderByDescending(i => i.IssuedAtUtc)
            .Skip(skip)
            .Take(Math.Clamp(request.PageSize, 1, 100))
            .Select(i => new BillingInvoiceListItemDto(
                i.Id,
                i.TenantId == null ? null : i.TenantId.Value,
                i.UserId,
                i.InvoiceNumber,
                i.Description,
                i.AmountCents,
                i.AmountCredits,
                i.Currency,
                i.Status.ToString(),
                i.Provider,
                i.IssuedAtUtc))
            .ToListAsync(cancellationToken);

        return rows;
    }
}
