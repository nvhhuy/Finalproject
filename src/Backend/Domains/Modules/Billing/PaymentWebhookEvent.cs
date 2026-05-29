using Domains.Common;

namespace Domains.Billing;

public enum PaymentWebhookStatus
{
    Received = 0,
    Processed = 1,
    Failed = 2,
    Ignored = 3,
}

/// <summary>Inbox row for payment-provider webhooks (Stripe, VNPay, …). Processed asynchronously in later phases.</summary>
public sealed class PaymentWebhookEvent : Entity<Guid>
{
    public string Provider { get; private set; } = null!;
    public string ExternalEventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public string PayloadJson { get; private set; } = null!;
    public PaymentWebhookStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    private PaymentWebhookEvent() { }

    private PaymentWebhookEvent(
        Guid id,
        string provider,
        string externalEventId,
        string eventType,
        string payloadJson,
        DateTime receivedAtUtc) : base(id)
    {
        Provider = provider;
        ExternalEventId = externalEventId;
        EventType = eventType;
        PayloadJson = payloadJson;
        Status = PaymentWebhookStatus.Received;
        ReceivedAtUtc = receivedAtUtc;
    }

    public static PaymentWebhookEvent Create(
        string provider,
        string externalEventId,
        string eventType,
        string payloadJson,
        DateTime receivedAtUtc) =>
        new(
            Guid.NewGuid(),
            provider.Trim().ToLowerInvariant(),
            externalEventId.Trim(),
            eventType.Trim(),
            payloadJson,
            receivedAtUtc);

    public void MarkProcessed(DateTime utcNow)
    {
        Status = PaymentWebhookStatus.Processed;
        ProcessedAtUtc = utcNow;
        ErrorMessage = null;
    }

    public void MarkIgnored(string reason, DateTime utcNow)
    {
        Status = PaymentWebhookStatus.Ignored;
        ProcessedAtUtc = utcNow;
        ErrorMessage = reason;
    }

    public void MarkFailed(string error, DateTime utcNow)
    {
        Status = PaymentWebhookStatus.Failed;
        ProcessedAtUtc = utcNow;
        ErrorMessage = error;
    }
}
