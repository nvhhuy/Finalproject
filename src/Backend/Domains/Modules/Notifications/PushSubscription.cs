namespace Domains.Notifications;

/// <summary>Browser Web Push subscription for a user (VAPID).</summary>
public sealed class PushSubscription
{
    private PushSubscription()
    {
    }

    public PushSubscription(Guid userId, string endpoint, string p256dh, string auth, string? userAgent = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        UserAgent = userAgent;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public string P256dh { get; private set; } = null!;
    public string Auth { get; private set; } = null!;
    public string? UserAgent { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
