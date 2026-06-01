using Application.Contracts.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications;

public sealed class NotificationDeliveryProcessor : INotificationDeliveryProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;

    public NotificationDeliveryProcessor(ApplicationDbContext db, INotificationDispatcher dispatcher)
    {
        _db = db;
        _dispatcher = dispatcher;
    }

    public async Task<bool> TryDeliverAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        // Guard: never mark as live-delivered if the host has no real push dispatcher.
        // NullNotificationDispatcher (Worker) always succeeds silently — if we let it mark delivered,
        // users would never receive the realtime push but retries would stop.
        if (!_dispatcher.IsRealtime)
            return false;

        var row = await _db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (row is null || row.LiveDeliveredAtUtc is not null)
            return false;

        try
        {
            await _dispatcher.DispatchAsync(
                row.UserId,
                new LiveNotificationPayload(
                    row.Id,
                    row.Title,
                    row.Body,
                    row.LinkUrl,
                    row.CreatedAtUtc),
                cancellationToken);
            row.MarkLiveDelivered(DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}