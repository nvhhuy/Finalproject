using Application.Contracts.Notifications;
using Application.Modules.Notifications;
using Domains.Notifications;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Modules.Notifications;

public sealed class MemberNotificationBroadcastProcessor : IMemberNotificationBroadcastProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationDeliveryProcessor _delivery;
    private readonly MemberNotificationBroadcastOptions _options;

    public MemberNotificationBroadcastProcessor(
        ApplicationDbContext db,
        IOptions<MemberNotificationBroadcastOptions> options,
        INotificationDeliveryProcessor delivery)
    {
        _db = db;
        _options = options.Value;
        _delivery = delivery;
    }

    public async Task<int> ProcessAsync(
        MemberNotificationBroadcastPayload payload,
        CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Clamp(_options.InsertBatchSize, 1, 2_000);
        var title = payload.Title.Trim();
        var body = payload.Body.Trim();
        var linkUrl = string.IsNullOrWhiteSpace(payload.LinkUrl) ? null : payload.LinkUrl.Trim();
        var inserted = 0;
        var offset = 0;
        var deliverAfterSave = new List<UserNotification>();

        while (true)
        {
            var memberIds = await LoadMemberIdBatchAsync(offset, batchSize, cancellationToken);
            if (memberIds.Count == 0)
                break;

            offset += memberIds.Count;
            var memberUserIds = memberIds.Select(id => id.Value).ToList();
            var alreadyNotified = await _db.UserNotifications
                .AsNoTracking()
                .Where(n => n.BroadcastEventId == payload.EventId && memberUserIds.Contains(n.UserId))
                .Select(n => n.UserId)
                .ToListAsync(cancellationToken);
            var skip = alreadyNotified.Count > 0 ? alreadyNotified.ToHashSet() : null;

            foreach (var userId in memberUserIds)
            {
                if (skip is not null && skip.Contains(userId))
                    continue;

                var row = new UserNotification(
                    userId,
                    title,
                    body,
                    linkUrl,
                    payload.EventId);
                _db.UserNotifications.Add(row);
                deliverAfterSave.Add(row);
                inserted++;
            }

            if (_db.ChangeTracker.HasChanges())
            {
                await _db.SaveChangesAsync(cancellationToken);
                await TryDeliverLiveAsync(deliverAfterSave, cancellationToken);
                deliverAfterSave.Clear();
                _db.ChangeTracker.Clear();
            }
        }

        return inserted;
    }

    private async Task TryDeliverLiveAsync(
        IReadOnlyList<UserNotification> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return;

        foreach (var row in rows)
            await _delivery.TryDeliverAsync(row.Id, cancellationToken);
    }

    private async Task<List<UserId>> LoadMemberIdBatchAsync(
        int offset,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var filter = _db.Users
            .AsNoTracking()
            .Where(u =>
                u.Role == UserRoles.User
                && u.DeletedAtUtc == null
                && u.AccountClosedAtUtc == null
                && !u.IsLocked);

        if (_db.Database.IsRelational())
        {
            return await filter
                .OrderBy(u => u.Id)
                .Skip(offset)
                .Take(batchSize)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        var members = await filter.ToListAsync(cancellationToken);
        return members
            .OrderBy(u => u.Id.Value)
            .Skip(offset)
            .Take(batchSize)
            .Select(u => u.Id)
            .ToList();
    }
}
