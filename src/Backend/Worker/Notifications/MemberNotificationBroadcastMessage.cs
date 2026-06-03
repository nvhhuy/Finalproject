namespace Worker.Notifications;

/// <summary>
///     Wire shape for <c>notifications.broadcast.requested.v1</c> (camelCase JSON from outbox).
/// </summary>
public sealed record MemberNotificationBroadcastMessage(
    Guid EventId,
    DateTime OccurredOnUtc,
    string Title,
    string Body,
    string? LinkUrl);
