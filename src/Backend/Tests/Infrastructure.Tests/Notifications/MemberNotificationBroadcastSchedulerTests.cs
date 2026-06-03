using Application.Contracts.Messaging;
using Application.Modules.Notifications;
using Domains.Notifications.Events;
using Infrastructure.Modules.Notifications;
using Infrastructure.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.Tests.Notifications;

public sealed class MemberNotificationBroadcastSchedulerTests
{
    [Fact]
    public async Task Broadcast_enqueues_outbox_without_inline_fan_out()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        db.Users.Add(NewMemberUser());
        await db.SaveChangesAsync();

        var outbox = new Mock<IIntegrationEventOutboxWriter>();
        MemberNotificationBroadcastRequestedIntegrationEvent? captured = null;
        outbox.Setup(o => o.EnqueueAsync(It.IsAny<Domains.Common.Interfaces.IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<Domains.Common.Interfaces.IIntegrationEvent, CancellationToken>((evt, _) =>
                captured = (MemberNotificationBroadcastRequestedIntegrationEvent)evt)
            .Returns(Task.CompletedTask);

        var processor = new Mock<Application.Contracts.Notifications.IMemberNotificationBroadcastProcessor>();
        var scheduler = new MemberNotificationBroadcastScheduler(
            db,
            outbox.Object,
            processor.Object,
            Options.Create(new MemberNotificationBroadcastOptions()));

        var count = await scheduler.BroadcastToAllMembersAsync("Hi", "There", "/app");

        Assert.Equal(1, count);
        Assert.NotNull(captured);
        Assert.Equal("Hi", captured!.Title);
        processor.Verify(
            p => p.ProcessAsync(It.IsAny<Application.Contracts.Notifications.MemberNotificationBroadcastPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Broadcast_runs_inline_processor_when_integration_flag_set()
    {
        using var db = TestDbContextFactory.Create(out _, out _);
        db.Users.Add(NewMemberUser());
        await db.SaveChangesAsync();

        var outbox = new Mock<IIntegrationEventOutboxWriter>();
        outbox.Setup(o => o.EnqueueAsync(It.IsAny<Domains.Common.Interfaces.IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var processor = new Mock<Application.Contracts.Notifications.IMemberNotificationBroadcastProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<Application.Contracts.Notifications.MemberNotificationBroadcastPayload>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var scheduler = new MemberNotificationBroadcastScheduler(
            db,
            outbox.Object,
            processor.Object,
            Options.Create(new MemberNotificationBroadcastOptions { ProcessInlineInIntegrationTests = true }));

        await scheduler.BroadcastToAllMembersAsync("Hi", "There", null);

        processor.Verify(
            p => p.ProcessAsync(It.IsAny<Application.Contracts.Notifications.MemberNotificationBroadcastPayload>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Domains.UserAggregate.User NewMemberUser() =>
        Domains.UserAggregate.User.CreateNew("A", "B", $"u{Guid.NewGuid():N}"[..12], $"{Guid.NewGuid():N}@example.com",
            "hash:placeholder");
}
