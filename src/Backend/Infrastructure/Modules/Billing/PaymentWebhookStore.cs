using Application.Contracts.Billing;
using Domains.Billing;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Billing;

public sealed class PaymentWebhookStore : IPaymentWebhookStore
{
    private readonly ApplicationDbContext _db;

    public PaymentWebhookStore(ApplicationDbContext db) => _db = db;

    public Task<PaymentWebhookEvent?> FindByExternalIdAsync(
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        _db.PaymentWebhookEvents.AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Provider == provider && e.ExternalEventId == externalEventId,
                cancellationToken);

    public void Add(PaymentWebhookEvent webhookEvent) => _db.PaymentWebhookEvents.Add(webhookEvent);
}
