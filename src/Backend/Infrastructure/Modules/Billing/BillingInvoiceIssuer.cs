using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class BillingInvoiceIssuer : IBillingInvoiceIssuer
{
    private readonly ApplicationDbContext _db;
    private readonly IApplicationReadDbContext _read;
    private readonly IBillingReceiptNotifier _receipts;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public BillingInvoiceIssuer(
        ApplicationDbContext db,
        IApplicationReadDbContext read,
        IBillingReceiptNotifier receipts,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _db = db;
        _read = read;
        _receipts = receipts;
        _uow = uow;
        _clock = clock;
    }

    public async Task<BillingInvoiceDto> IssueSubscriptionInvoiceAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        Plan? plan = await _read.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
        int amount = plan?.PriceMonthlyCents ?? 0;
        string currency = plan?.Currency ?? "USD";
        string desc = plan is null
            ? "Subscription"
            : $"Subscription — {plan.Name} ({plan.Code})";

        return await IssueAsync(
            TenantId.Create(tenantId),
            subscriptionId,
            userId: null,
            desc,
            amount,
            amountCredits: null,
            currency,
            provider,
            cancellationToken);
    }

    public Task<BillingInvoiceDto> IssuePlanChangeInvoiceAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string provider,
        CancellationToken cancellationToken = default) =>
        IssueSubscriptionInvoiceAsync(tenantId, subscriptionId, planId, provider, cancellationToken);

    public async Task<BillingInvoiceDto> IssueWalletTopUpReceiptAsync(
        Guid userId,
        long amountCredits,
        int amountCents,
        string currency,
        string provider,
        CancellationToken cancellationToken = default)
    {
        string desc = $"Wallet top-up — {amountCredits} credits";
        return await IssueAsync(
            tenantId: null,
            subscriptionId: null,
            userId,
            desc,
            amountCents,
            amountCredits,
            currency,
            provider,
            cancellationToken);
    }

    private async Task<BillingInvoiceDto> IssueAsync(
        TenantId? tenantId,
        Guid? subscriptionId,
        Guid? userId,
        string description,
        int amountCents,
        long? amountCredits,
        string currency,
        string provider,
        CancellationToken cancellationToken)
    {
        DateTime now = _clock.Now;
        string number = BillingInvoice.GenerateInvoiceNumber(now);
        BillingInvoice invoice = tenantId is not null
            ? BillingInvoice.ForSubscription(
                tenantId,
                subscriptionId!.Value,
                number,
                description,
                amountCents,
                currency,
                provider,
                now)
            : BillingInvoice.ForWalletTopUp(
                userId!.Value,
                number,
                description,
                amountCents,
                amountCredits!.Value,
                currency,
                provider,
                now);

        _db.BillingInvoices.Add(invoice);
        await _uow.SaveChangesAsync(cancellationToken);

        BillingInvoiceDto dto = ToDto(invoice);
        string? email = await ResolveRecipientEmailAsync(tenantId, userId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(email))
            await _receipts.SendReceiptAsync(dto, email, cancellationToken);

        return dto;
    }

    private async Task<string?> ResolveRecipientEmailAsync(
        TenantId? tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (tenantId is not null)
        {
            return await _read.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Join(_read.Users.AsNoTracking(), t => t.OwnerUserId, u => u.Id, (_, u) => u.Email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (userId is not null)
        {
            Domains.UserAggregate.ValueObjects.UserId userKey =
                Domains.UserAggregate.ValueObjects.UserId.Create(userId.Value);
            return await _read.Users.AsNoTracking()
                .Where(u => u.Id == userKey)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private static BillingInvoiceDto ToDto(BillingInvoice invoice) =>
        new(
            invoice.Id,
            invoice.TenantId?.Value,
            invoice.UserId,
            invoice.InvoiceNumber,
            invoice.Description,
            invoice.AmountCents,
            invoice.AmountCredits,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.Provider,
            invoice.IssuedAtUtc);
}
