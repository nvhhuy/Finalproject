namespace Application.Contracts.Notifications;

/// <summary>
/// Creates in-app notifications for all active member accounts. Live delivery is handled
/// asynchronously by <see cref="INotificationDeliveryProcessor"/>.
/// </summary>
public interface IMemberNotificationBroadcaster
{
    Task<int> BroadcastToAllMembersAsync(
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default);
}
