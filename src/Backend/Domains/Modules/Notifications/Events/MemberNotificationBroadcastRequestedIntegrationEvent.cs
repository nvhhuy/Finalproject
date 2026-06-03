using Domains.Common.Interfaces;

namespace Domains.Notifications.Events;

/// <summary>
///     Queued by the admin broadcast API; a worker consumer fans out in-app rows in bounded batches.
/// </summary>
public sealed record MemberNotificationBroadcastRequestedIntegrationEvent : IIntegrationEvent
{
    public MemberNotificationBroadcastRequestedIntegrationEvent(string title, string body, string? linkUrl = null)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        Title = title;
        Body = body;
        LinkUrl = linkUrl;
    }

    public string Title { get; init; } = null!;
    public string Body { get; init; } = null!;
    public string? LinkUrl { get; init; }

    public Guid EventId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public string RoutingKey => "notifications.broadcast.requested.v1";
}
