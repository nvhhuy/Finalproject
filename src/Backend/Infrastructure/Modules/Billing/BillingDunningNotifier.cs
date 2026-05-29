using Application.Contracts.Billing;
using Application.Contracts.Caching;
using Application.Contracts.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Modules.Billing;

public sealed class BillingDunningNotifier : IBillingDunningNotifier
{
    private static readonly TimeSpan DunningThrottle = TimeSpan.FromHours(24);

    private readonly IEmailSender _email;
    private readonly ICacheService _cache;
    private readonly ILogger<BillingDunningNotifier> _logger;

    public BillingDunningNotifier(
        IEmailSender email,
        ICacheService cache,
        ILogger<BillingDunningNotifier> logger)
    {
        _email = email;
        _cache = cache;
        _logger = logger;
    }

    public async Task NotifyPastDueAsync(
        Guid tenantId,
        Guid subscriptionId,
        string tenantDisplayName,
        string ownerEmail,
        string planName,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = $"billing:dunning:{subscriptionId:N}";
        if (await _cache.GetAsync<string>(cacheKey, cancellationToken) is not null)
            return;

        string subject = $"Action required: subscription past due for {tenantDisplayName}";
        string html = $"""
            <p>Hello,</p>
            <p>Your organization <strong>{tenantDisplayName}</strong> subscription ({planName}) is past due
            since {periodEndUtc:u}.</p>
            <p>Renew or update your plan in the app to restore full access. Write operations are currently read-only.</p>
            <p>— Billing</p>
            """;

        await _email.SendAsync(ownerEmail, subject, html, cancellationToken);
        await _cache.SetAsync(cacheKey, "1", DunningThrottle, cancellationToken);
        _logger.LogInformation(
            "Sent dunning email for tenant {TenantId} subscription {SubscriptionId} to {Email}.",
            tenantId,
            subscriptionId,
            ownerEmail);
    }
}
