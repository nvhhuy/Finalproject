namespace Application.Contracts.Notifications;

public interface INotificationDeliveryProcessor
{
    Task<bool> TryDeliverAsync(Guid notificationId, CancellationToken cancellationToken = default);
}