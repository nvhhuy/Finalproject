using Domains.Billing;

namespace Application.Contracts.Billing;

public interface IPaymentWebhookStore
{
    Task<PaymentWebhookEvent?> FindByExternalIdAsync(
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default);

    void Add(PaymentWebhookEvent webhookEvent);
}