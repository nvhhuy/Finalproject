namespace Application.Contracts.Notifications;

public interface IUserNotificationWriter
{
    Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
}