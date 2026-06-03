namespace Application.Contracts.Notifications;

/// <summary>
///     Queues a member-wide in-app announcement via the transactional outbox. Fan-out runs in the
///     worker (or inline during integration tests). Live SignalR/Web Push delivery is handled by
///     <see cref="INotificationDeliveryProcessor" />.
/// </summary>
public interface IMemberNotificationBroadcaster
{
    /// <summary>
    ///     Returns the number of active members that will receive the announcement once fan-out completes.
    /// </summary>
    Task<int> BroadcastToAllMembersAsync(
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default);
}
