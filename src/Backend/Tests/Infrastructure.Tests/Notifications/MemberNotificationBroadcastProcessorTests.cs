using Application.Contracts.Notifications;
using Application.Modules.Notifications;
using Moq;
using Domains.UserAggregate;
using Infrastructure.Modules.Notifications;
using Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tests.Notifications;

public sealed class MemberNotificationBroadcastProcessorTests
{
    [Fact]
    public async Task ProcessAsync_inserts_one_notification_per_active_member()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var member = NewMemberUser();
        var locked = NewMemberUser();
        locked.Lock("test");
        db.Users.AddRange(member, locked);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(db, batchSize: 500);
        var eventId = Guid.NewGuid();
        var inserted = await processor.ProcessAsync(
            new MemberNotificationBroadcastPayload(eventId, "Title", "Body", "/app"));

        Assert.Equal(1, inserted);
        var rows = await db.UserNotifications.Where(n => n.BroadcastEventId == eventId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(member.Id.Value, rows[0].UserId);
    }

    [Fact]
    public async Task ProcessAsync_is_idempotent_for_same_event_id()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        var member = NewMemberUser();
        db.Users.Add(member);
        await db.SaveChangesAsync();

        var processor = CreateProcessor(db);
        var payload = new MemberNotificationBroadcastPayload(Guid.NewGuid(), "T", "B", null);

        var first = await processor.ProcessAsync(payload);
        var second = await processor.ProcessAsync(payload);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, await db.UserNotifications.CountAsync(n => n.BroadcastEventId == payload.EventId));
    }

    [Fact]
    public async Task ProcessAsync_pages_members_in_batches()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        for (var i = 0; i < 5; i++)
            db.Users.Add(NewMemberUser());
        await db.SaveChangesAsync();

        var processor = CreateProcessor(db, batchSize: 2);
        var eventId = Guid.NewGuid();
        var inserted = await processor.ProcessAsync(
            new MemberNotificationBroadcastPayload(eventId, "Batch", "Test", null));

        Assert.Equal(5, inserted);
        Assert.Equal(5, await db.UserNotifications.CountAsync(n => n.BroadcastEventId == eventId));
    }

    private static MemberNotificationBroadcastProcessor CreateProcessor(
        Infrastructure.Persistence.ApplicationDbContext db,
        int batchSize = 500)
    {
        var delivery = new Mock<INotificationDeliveryProcessor>();
        delivery
            .Setup(d => d.TryDeliverAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new(
            db,
            Options.Create(new MemberNotificationBroadcastOptions { InsertBatchSize = batchSize }),
            delivery.Object);
    }

    private static User NewMemberUser() =>
        User.CreateNew("A", "B", $"u{Guid.NewGuid():N}"[..12], $"{Guid.NewGuid():N}@example.com", "hash:placeholder");
}
