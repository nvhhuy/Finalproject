namespace Application.Modules.Notifications;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public bool Enabled { get; set; }

    /// <summary>URL-safe base64 public VAPID key (sent to browsers).</summary>
    public string VapidPublicKey { get; set; } = "";

    /// <summary>URL-safe base64 private VAPID key.</summary>
    public string VapidPrivateKey { get; set; } = "";

    /// <summary>mailto: or https: contact for push service (required by spec).</summary>
    public string Subject { get; set; } = "mailto:support@localhost";
}
