using Application.Contracts.Notifications;
using Domains.Notifications;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications;

public sealed class UserNotificationService : IUserNotificationService, IUserNotificationWriter
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDispatcher _dispatcher;

    public UserNotificationService(ApplicationDbContext db, INotificationDispatcher dispatcher)
    {
        _db = db;
        _dispatcher = dispatcher;
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

        try
        {
            await _dispatcher.DispatchAsync(
                userId,
                new LiveNotificationPayload(
                    notification.Id,
                    notification.Title,
                    notification.Body,
                    notification.LinkUrl,
                    notification.CreatedAtUtc),
                cancellationToken);
            notification.MarkLiveDelivered(DateTime.UtcNow);
        }
        catch
        {
            // Worker job retries undelivered notifications.
        }

        return notification.Id;
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        UserNotification? row = await _db.UserNotifications
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
