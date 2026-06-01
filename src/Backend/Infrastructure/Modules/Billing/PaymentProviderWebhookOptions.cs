namespace Infrastructure.Modules.Billing;

/// <summary>HMAC-SHA256 signature options for VNPay IPN webhooks.</summary>
public sealed class VnPayWebhookOptions
{
    public const string SectionName = "VnPay";

    /// <summary>
    ///     VNPay hash secret. When set, the controller validates the <c>X-VNPay-Signature</c>
    ///     header (HMAC-SHA256 of the raw request body). Empty = accept all in dev only.
    /// </summary>
    public string? WebhookSecret { get; set; }

    public bool RequireSignature => !string.IsNullOrWhiteSpace(WebhookSecret);
}

/// <summary>HMAC-SHA256 signature options for PayOS webhooks.</summary>
public sealed class PayOsWebhookOptions
{
    public const string SectionName = "PayOs";

    /// <summary>
    ///     PayOS checksum key. When set, the controller validates the <c>X-PayOS-Signature</c>
    ///     header (HMAC-SHA256 of the raw request body). Empty = accept all in dev only.
    /// </summary>
    public string? WebhookSecret { get; set; }

    public bool RequireSignature => !string.IsNullOrWhiteSpace(WebhookSecret);
}
