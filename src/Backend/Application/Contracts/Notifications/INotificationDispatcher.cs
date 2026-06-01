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
    /// <summary>
    ///     <c>true</c> when this dispatcher can actually reach connected clients.
    ///     <c>false</c> for the no-op implementation registered on hosts without a SignalR hub (e.g. Worker).
    ///     <see cref="INotificationDeliveryProcessor" /> uses this to avoid marking notifications as
    ///     live-delivered when no real push was made.
    /// </summary>
    bool IsRealtime { get; }

    Task DispatchAsync(Guid userId, LiveNotificationPayload payload, CancellationToken cancellationToken = default);
}