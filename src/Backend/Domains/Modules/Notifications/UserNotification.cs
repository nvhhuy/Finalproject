namespace Domains.Notifications;

/// <summary>In-app notification persisted for a user; pushed live via SignalR when enabled.</summary>
public sealed class UserNotification
{
    private UserNotification() { }

    public UserNotification(Guid userId, string title, string body, string? linkUrl = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title;
        Body = body;
        LinkUrl = linkUrl;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? LinkUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    public void MarkRead(DateTime utcNow) => ReadAtUtc = utcNow;
}
