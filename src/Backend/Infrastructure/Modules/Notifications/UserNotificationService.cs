using Application.Contracts.Notifications;
using Domains.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications;

public sealed class UserNotificationService : IUserNotificationService, IUserNotificationWriter
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IWebPushNotificationSender _webPush;

    public UserNotificationService(
        ApplicationDbContext db,
        INotificationDispatcher dispatcher,
        IWebPushNotificationSender webPush)
    {
        _db = db;
        _dispatcher = dispatcher;
        _webPush = webPush;
    }

    public async Task<Guid> CreateAndDispatchAsync(
        Guid userId,
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new UserNotification(userId, title, body, linkUrl);
        _db.Set<UserNotification>().Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        // Only attempt live dispatch and mark delivered when a real push channel exists.
        // NullNotificationDispatcher (Api with Realtime:Enabled=false, or Worker host) always
        // succeeds silently — letting it mark delivered stops the Worker retry job from
        // re-attempting when the user was never actually pushed.
        var payload = new LiveNotificationPayload(
            notification.Id,
            notification.Title,
            notification.Body,
            notification.LinkUrl,
            notification.CreatedAtUtc);

        var liveDelivered = false;

        if (_dispatcher.IsRealtime)
        {
            try
            {
                await _dispatcher.DispatchAsync(userId, payload, cancellationToken);
                liveDelivered = true;
            }
            catch
            {
                // Worker job retries undelivered notifications.
            }
        }

        if (_webPush.IsEnabled)
        {
            try
            {
                if (await _webPush.SendToUserAsync(userId, payload, cancellationToken))
                    liveDelivered = true;
            }
            catch
            {
                // Best-effort; cron retries undelivered rows.
            }
        }

        if (liveDelivered)
            notification.MarkLiveDelivered(DateTime.UtcNow);

        return notification.Id;
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (row is null)
            return false;

        if (!row.IsRead)
        {
            row.MarkRead(DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}