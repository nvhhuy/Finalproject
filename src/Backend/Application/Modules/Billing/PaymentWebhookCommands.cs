using System.Text.Json;
using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Application.Contracts.Wallet;
using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Billing;

public sealed record ReceivePaymentWebhookCommand(
    string Provider,
    string PayloadJson,
    string? ExternalEventId,
    string? EventType) : IRequest<Result<PaymentWebhookReceiptDto, Error>>;

public sealed record PaymentWebhookReceiptDto(
    Guid EventId,
    bool Duplicate,
    string Status);

public sealed class ReceivePaymentWebhookCommandValidator : AbstractValidator<ReceivePaymentWebhookCommand>
{
    public ReceivePaymentWebhookCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PayloadJson).NotEmpty();
    }
}

public sealed class ReceivePaymentWebhookCommandHandler
    : IRequestHandler<ReceivePaymentWebhookCommand, Result<PaymentWebhookReceiptDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly ICreditPackageStore _creditPackages;
    private readonly IApplicationReadDbContext _db;
    private readonly IBillingDunningNotifier _dunning;
    private readonly IBillingInvoiceIssuer _invoices;
    private readonly ISender _sender;
    private readonly ISubscriptionStore _subscriptions;
    private readonly IUnitOfWork _uow;
    private readonly IWalletService _wallet;
    private readonly IPaymentWebhookStore _webhooks;

    public ReceivePaymentWebhookCommandHandler(
        IPaymentWebhookStore webhooks,
        ISender sender,
        IBillingInvoiceIssuer invoices,
        ISubscriptionStore subscriptions,
        IBillingDunningNotifier dunning,
        ICreditPackageStore creditPackages,
        IWalletService wallet,
        IApplicationReadDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _webhooks = webhooks;
        _sender = sender;
        _invoices = invoices;
        _subscriptions = subscriptions;
        _dunning = dunning;
        _creditPackages = creditPackages;
        _wallet = wallet;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<PaymentWebhookReceiptDto, Error>> Handle(
        ReceivePaymentWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var provider = request.Provider.Trim().ToLowerInvariant();
        var externalId = string.IsNullOrWhiteSpace(request.ExternalEventId)
            ? $"generated:{Guid.NewGuid():N}"
            : request.ExternalEventId.Trim();
        var eventType = string.IsNullOrWhiteSpace(request.EventType) ? "unknown" : request.EventType.Trim();

        var existing = await _webhooks.FindByExternalIdAsync(provider, externalId, cancellationToken);
        if (existing is not null) return new PaymentWebhookReceiptDto(existing.Id, true, existing.Status.ToString());

        var webhook = PaymentWebhookEvent.Create(
            provider,
            externalId,
            eventType,
            request.PayloadJson,
            _clock.Now);

        if (provider == "stripe" && eventType == "checkout.session.completed")
        {
            var walletPurchase =
                WalletPurchaseWebhookMetadata.TryParse(request.PayloadJson);
            if (walletPurchase is not null)
            {
                await FulfillWalletPurchaseAsync(webhook, walletPurchase, provider, externalId, cancellationToken);
            }
            else
            {
                var checkout = StripeCheckoutMetadata.TryParseFromStripePayload(request.PayloadJson);
                if (checkout is null)
                {
                    webhook.MarkIgnored("checkout.session.completed without provisioning metadata.", _clock.Now);
                }
                else
                {
                    var provision = await _sender.Send(
                        new SubscribeToPlanCommand(
                            checkout.UserId,
                            checkout.PlanCode,
                            checkout.TenantSlug,
                            checkout.TenantDisplayName,
                            checkout.PublicTagline,
                            checkout.PublicDescription),
                        cancellationToken);
                    if (provision.IsFailure)
                    {
                        webhook.MarkFailed(provision.Error.ErrorMessage ?? "Provisioning failed.", _clock.Now);
                    }
                    else
                    {
                        var planCode = Plan.NormalizeCode(checkout.PlanCode);
                        var plan = await _db.Plans.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.Code == planCode, cancellationToken);
                        if (plan is not null)
                            await _invoices.IssueSubscriptionInvoiceAsync(
                                provision.Value.TenantId,
                                provision.Value.SubscriptionId,
                                plan.Id,
                                provider,
                                cancellationToken);

                        webhook.MarkProcessed(_clock.Now);
                    }
                }
            }
        }
        else if (provider == "stripe" && eventType == "invoice.payment_failed")
        {
            await HandlePaymentFailedAsync(webhook, request.PayloadJson, cancellationToken);
        }
        else if (provider == "stripe" && eventType == "invoice.paid")
        {
            webhook.MarkIgnored("invoice.paid — subscription renewals not implemented in MVP.", _clock.Now);
        }
        else if (IsWalletPaymentSuccess(eventType))
        {
            var walletPurchase =
                WalletPurchaseWebhookMetadata.TryParse(request.PayloadJson);
            if (walletPurchase is null)
                webhook.MarkIgnored("payment.success without wallet_topup metadata.", _clock.Now);
            else
                await FulfillWalletPurchaseAsync(webhook, walletPurchase, provider, externalId, cancellationToken);
        }
        else
        {
            webhook.MarkProcessed(_clock.Now);
        }

        _webhooks.Add(webhook);
        await _uow.SaveChangesAsync(cancellationToken);

        return new PaymentWebhookReceiptDto(webhook.Id, false, webhook.Status.ToString());
    }

    private static bool IsWalletPaymentSuccess(string eventType)
    {
        return eventType.Equals("payment.success", StringComparison.OrdinalIgnoreCase)
               || eventType.Equals("payment.succeeded", StringComparison.OrdinalIgnoreCase);
    }

    private async Task FulfillWalletPurchaseAsync(
        PaymentWebhookEvent webhook,
        WalletPurchaseWebhookMetadata metadata,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken)
    {
        var package = await _creditPackages.GetByCodeAsync(metadata.PackageCode, true, cancellationToken);
        if (package is null)
        {
            webhook.MarkFailed($"Unknown credit package '{metadata.PackageCode}'.", _clock.Now);
            return;
        }

        var idempotencyKey = $"webhook:{provider}:{externalEventId}";
        await _wallet.PurchasePackageAsync(
            metadata.UserId,
            package.Id,
            package.Code,
            package.CreditsAmount,
            package.PriceCents,
            package.Currency,
            idempotencyKey,
            provider,
            cancellationToken);
        webhook.MarkProcessed(_clock.Now);
    }

    private async Task HandlePaymentFailedAsync(
        PaymentWebhookEvent webhook,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var tenantId = TryParseTenantIdFromStripePayload(payloadJson);
        if (tenantId is null)
        {
            webhook.MarkIgnored("invoice.payment_failed without tenant_id metadata.", _clock.Now);
            return;
        }

        var subscription = await _subscriptions.GetByTenantForUpdateAsync(
            TenantId.Create(tenantId.Value),
            cancellationToken);
        if (subscription is null)
        {
            webhook.MarkFailed("No subscription for tenant.", _clock.Now);
            return;
        }

        subscription.MarkPastDue(_clock.Now);
        webhook.MarkProcessed(_clock.Now);

        var row = await (
                from t in _db.Tenants.AsNoTracking()
                join u in _db.Users.AsNoTracking() on t.OwnerUserId equals u.Id
                join p in _db.Plans.AsNoTracking() on subscription.PlanId equals p.Id
                where t.Id == subscription.TenantId
                select new { t.DisplayName, u.Email, p.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is not null && !string.IsNullOrWhiteSpace(row.Email))
            await _dunning.NotifyPastDueAsync(
                tenantId.Value,
                subscription.Id,
                row.DisplayName,
                row.Email,
                row.Name,
                subscription.CurrentPeriodEndUtc,
                cancellationToken);
    }

    private static Guid? TryParseTenantIdFromStripePayload(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;
            if (!data.TryGetProperty("object", out var obj))
                return null;
            if (obj.TryGetProperty("metadata", out var metadata) &&
                metadata.TryGetProperty("tenant_id", out var tid) &&
                tid.ValueKind == JsonValueKind.String &&
                Guid.TryParse(tid.GetString(), out var fromMeta))
                return fromMeta;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}

public sealed record GetTenantEntitlementsQuery(Guid TenantId, Guid UserId)
    : IRequest<Result<TenantEntitlementsDto, Error>>;

public sealed record TenantEntitlementsDto(
    SubscriptionSummaryDto Subscription,
    PlanLimits Limits,
    TenantUsageSnapshot Usage,
    bool AllowsWriteAccess);

public sealed class GetTenantEntitlementsQueryHandler
    : IRequestHandler<GetTenantEntitlementsQuery, Result<TenantEntitlementsDto, Error>>
{
    private readonly IEntitlementService _entitlements;
    private readonly ITenantMembershipService _membership;
    private readonly ISender _sender;

    public GetTenantEntitlementsQueryHandler(
        IEntitlementService entitlements,
        ISender sender,
        ITenantMembershipService membership)
    {
        _entitlements = entitlements;
        _sender = sender;
        _membership = membership;
    }

    public async Task<Result<TenantEntitlementsDto, Error>> Handle(
        GetTenantEntitlementsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _membership.IsMemberAsync(request.TenantId, request.UserId, cancellationToken))
            return new ForbiddenError("You are not a member of this tenant.");

        var subscription =
            await _sender.Send(new GetTenantSubscriptionQuery(request.TenantId), cancellationToken);
        if (subscription.IsFailure)
            return subscription.Error;

        var limits = await _entitlements.GetLimitsAsync(request.TenantId, cancellationToken);
        if (limits is null)
            return new NotFoundError("Plan limits were not found for this tenant.");

        var usage = await _entitlements.GetUsageAsync(request.TenantId, cancellationToken);
        var writeAccess = await _entitlements.AllowsWriteAccessAsync(request.TenantId, cancellationToken);

        return new TenantEntitlementsDto(subscription.Value, limits, usage, writeAccess);
    }
}