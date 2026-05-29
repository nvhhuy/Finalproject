using Domains.Common;
using Domains.Tenancy.ValueObjects;

namespace Domains.Billing;

public sealed class BillingInvoice : AggregateRoot<Guid>
{
    public TenantId? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public string InvoiceNumber { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int AmountCents { get; private set; }
    public long? AmountCredits { get; private set; }
    public string Currency { get; private set; } = "USD";
    public BillingInvoiceStatus Status { get; private set; }
    public string Provider { get; private set; } = null!;
    public string? ExternalReference { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }

    private BillingInvoice() { }

    private BillingInvoice(
        Guid id,
        TenantId? tenantId,
        Guid? userId,
        Guid? subscriptionId,
        string invoiceNumber,
        string description,
        int amountCents,
        long? amountCredits,
        string currency,
        string provider,
        string? externalReference,
        DateTime issuedAtUtc) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        SubscriptionId = subscriptionId;
        InvoiceNumber = invoiceNumber;
        Description = description;
        AmountCents = amountCents;
        AmountCredits = amountCredits;
        Currency = currency;
        Status = BillingInvoiceStatus.Paid;
        Provider = provider;
        ExternalReference = externalReference;
        IssuedAtUtc = issuedAtUtc;
    }

    public static BillingInvoice ForSubscription(
        TenantId tenantId,
        Guid subscriptionId,
        string invoiceNumber,
        string description,
        int amountCents,
        string currency,
        string provider,
        DateTime issuedAtUtc) =>
        new(
            Guid.NewGuid(),
            tenantId,
            userId: null,
            subscriptionId,
            invoiceNumber,
            description,
            amountCents,
            amountCredits: null,
            currency,
            provider,
            externalReference: null,
            issuedAtUtc);

    public static BillingInvoice ForWalletTopUp(
        Guid userId,
        string invoiceNumber,
        string description,
        int amountCents,
        long amountCredits,
        string currency,
        string provider,
        DateTime issuedAtUtc) =>
        new(
            Guid.NewGuid(),
            tenantId: null,
            userId,
            subscriptionId: null,
            invoiceNumber,
            description,
            amountCents,
            amountCredits,
            currency,
            provider,
            externalReference: null,
            issuedAtUtc);

    public static string GenerateInvoiceNumber(DateTime utcNow) =>
        $"INV-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}
