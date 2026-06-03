using Application.Contracts.Notifications;

namespace Infrastructure.Modules.Notifications;

public sealed class NullWebPushNotificationSender : IWebPushNotificationSender
{
    public bool IsEnabled => false;

    public Task<bool> SendToUserAsync(Guid userId, LiveNotificationPayload payload,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
