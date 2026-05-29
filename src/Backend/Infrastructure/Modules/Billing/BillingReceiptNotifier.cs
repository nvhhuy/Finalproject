using Application.Contracts.Billing;
using Application.Contracts.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Modules.Billing;

public sealed class BillingReceiptNotifier : IBillingReceiptNotifier
{
    private readonly IEmailSender _email;
    private readonly ILogger<BillingReceiptNotifier> _logger;

    public BillingReceiptNotifier(IEmailSender email, ILogger<BillingReceiptNotifier> logger)
    {
        _email = email;
        _logger = logger;
    }

    public async Task SendReceiptAsync(BillingInvoiceDto invoice, string recipientEmail, CancellationToken cancellationToken = default)
    {
        string amountLine = invoice.AmountCredits is not null
            ? $"{invoice.AmountCredits} credits"
            : $"{invoice.AmountCents / 100m:0.00} {invoice.Currency}";

        string subject = $"Receipt {invoice.InvoiceNumber}";
        string html = $"""
            <p>Thank you for your payment.</p>
            <p><strong>Invoice:</strong> {invoice.InvoiceNumber}<br/>
            <strong>Description:</strong> {invoice.Description}<br/>
            <strong>Amount:</strong> {amountLine}<br/>
            <strong>Status:</strong> {invoice.Status}<br/>
            <strong>Date:</strong> {invoice.IssuedAtUtc:u}</p>
            <p>— Billing</p>
            """;

        await _email.SendAsync(recipientEmail, subject, html, cancellationToken);
        _logger.LogInformation("Sent billing receipt {InvoiceNumber} to {Email}.", invoice.InvoiceNumber, recipientEmail);
    }
}
