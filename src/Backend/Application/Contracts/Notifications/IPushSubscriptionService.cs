namespace Application.Contracts.Notifications;

public interface IPushSubscriptionService
{
    string? GetVapidPublicKey();

    Task RegisterAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default);
}
