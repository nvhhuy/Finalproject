namespace Application.Contracts.Notifications;

public sealed record MemberNotificationBroadcastPayload(
    Guid EventId,
    string Title,
    string Body,
    string? LinkUrl);
