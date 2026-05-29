namespace Application.Contracts.Billing;

public interface IBillingReceiptNotifier
{
    Task SendReceiptAsync(BillingInvoiceDto invoice, string recipientEmail,
        CancellationToken cancellationToken = default);
}