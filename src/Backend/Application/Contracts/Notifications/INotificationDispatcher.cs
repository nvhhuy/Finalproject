namespace Application.Contracts.Notifications;

public sealed record LiveNotificationPayload(
    Guid Id,
    string Title,
    string Body,
    string? LinkUrl,
    DateTime CreatedAtUtc);

/// <summary>Pushes a notification to connected clients (SignalR when enabled).</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(Guid userId, LiveNotificationPayload payload, CancellationToken cancellationToken = default);
}