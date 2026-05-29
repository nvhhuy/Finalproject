namespace Infrastructure.Modules.Billing;

public sealed class StripeWebhookOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Stripe webhook signing secret (whsec_…). Empty = accept webhooks in dev only.</summary>
    public string? WebhookSecret { get; set; }

    public bool RequireSignature => !string.IsNullOrWhiteSpace(WebhookSecret);
}