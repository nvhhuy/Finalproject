using Application.Contracts.Notifications;
using Domains.Notifications;
using Domains.UserAggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications;

public sealed class MemberNotificationBroadcaster : IMemberNotificationBroadcaster
{
    private const int InsertBatchSize = 500;

    private readonly ApplicationDbContext _db;

    public MemberNotificationBroadcaster(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<int> BroadcastToAllMembersAsync(
        string title,
        string body,
        string? linkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var memberIds = await _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Role == UserRoles.User
                && u.DeletedAtUtc == null
                && u.AccountClosedAtUtc == null
                && !u.IsLocked)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var memberUserIds = memberIds.Select(id => id.Value).ToList();

        if (memberUserIds.Count == 0)
            return 0;

        for (var offset = 0; offset < memberUserIds.Count; offset += InsertBatchSize)
        {
            var batch = memberUserIds.Skip(offset).Take(InsertBatchSize);
            foreach (var userId in batch)
                _db.UserNotifications.Add(new UserNotification(userId, title, body, linkUrl));

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        return memberUserIds.Count;
    }
}