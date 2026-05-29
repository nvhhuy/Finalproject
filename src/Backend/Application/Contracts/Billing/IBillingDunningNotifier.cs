namespace Application.Contracts.Billing;

public interface IBillingDunningNotifier
{
    Task NotifyPastDueAsync(
        Guid tenantId,
        Guid subscriptionId,
        string tenantDisplayName,
        string ownerEmail,
        string planName,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);
}
