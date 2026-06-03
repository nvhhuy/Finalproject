using Application.Contracts.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications;

public sealed class NotificationDeliveryProcessor : INotificationDeliveryProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IWebPushNotificationSender _webPush;

    public NotificationDeliveryProcessor(
        ApplicationDbContext db,
        INotificationDispatcher dispatcher,
        IWebPushNotificationSender webPush)
    {
        _db = db;
        _dispatcher = dispatcher;
        _webPush = webPush;
    }

    public async Task<bool> TryDeliverAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        if (!_dispatcher.IsRealtime && !_webPush.IsEnabled)
            return false;

        var row = await _db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (row is null || row.LiveDeliveredAtUtc is not null)
            return false;

        var payload = new LiveNotificationPayload(
            row.Id,
            row.Title,
            row.Body,
            row.LinkUrl,
            row.CreatedAtUtc);

        var delivered = false;

        if (_dispatcher.IsRealtime)
        {
            try
            {
                await _dispatcher.DispatchAsync(row.UserId, payload, cancellationToken);
                delivered = true;
            }
            catch
            {
                // fall through to web push
            }
        }

        if (_webPush.IsEnabled)
        {
            try
            {
                if (await _webPush.SendToUserAsync(row.UserId, payload, cancellationToken))
                    delivered = true;
            }
            catch
            {
                // retry on next cron tick
            }
        }

        if (!delivered)
            return false;

        row.MarkLiveDelivered(DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}