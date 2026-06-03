namespace Application.Contracts.Notifications;

public interface IWebPushNotificationSender
{
    bool IsEnabled { get; }

    Task<bool> SendToUserAsync(Guid userId, LiveNotificationPayload payload,
        CancellationToken cancellationToken = default);
}
