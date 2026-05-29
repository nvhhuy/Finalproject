namespace Application.Contracts.Billing;

public sealed record BillingInvoiceDto(
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

public interface IBillingInvoiceIssuer
{
    Task<BillingInvoiceDto> IssueSubscriptionInvoiceAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string provider,
        CancellationToken cancellationToken = default);

    Task<BillingInvoiceDto> IssuePlanChangeInvoiceAsync(
        Guid tenantId,
        Guid subscriptionId,
        Guid planId,
        string provider,
        CancellationToken cancellationToken = default);

    Task<BillingInvoiceDto> IssueWalletTopUpReceiptAsync(
        Guid userId,
        long amountCredits,
        int amountCents,
        string currency,
        string provider,
        CancellationToken cancellationToken = default);
}
