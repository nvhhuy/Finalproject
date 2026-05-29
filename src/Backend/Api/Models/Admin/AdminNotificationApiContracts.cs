namespace Api.Models.Admin;

public sealed record BroadcastMemberNotificationRequest(string Title, string Body, string? LinkUrl = null);

public sealed record BroadcastMemberNotificationResponse(int RecipientCount);
