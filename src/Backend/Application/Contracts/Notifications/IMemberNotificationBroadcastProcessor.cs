namespace Application.Contracts.Notifications;

/// <summary>
///     Inserts in-app notifications for all active members in bounded batches (worker or inline test).
/// </summary>
public interface IMemberNotificationBroadcastProcessor
{
    Task<int> ProcessAsync(MemberNotificationBroadcastPayload payload, CancellationToken cancellationToken = default);
}
