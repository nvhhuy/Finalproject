using Application.Contracts.Administration;
using Application.Modules.Notifications.Admin;
using Application.Modules.Users.Administration;
using Moq;

namespace Application.Tests.Notifications;

public sealed class BroadcastMemberNotificationCommandHandlerTests
{
    [Fact]
    public async Task Handle_returns_validation_error_when_no_active_members()
    {
        var broadcaster = new Mock<Application.Contracts.Notifications.IMemberNotificationBroadcaster>();
        broadcaster.Setup(b => b.BroadcastToAllMembersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var audit = new Mock<IAdminAuditLogWriter>();
        var handler = new BroadcastMemberNotificationCommandHandler(broadcaster.Object, audit.Object);

        var result = await handler.Handle(
            new BroadcastMemberNotificationCommand(Guid.NewGuid(), "T", "B", null, "127.0.0.1"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        audit.Verify(a => a.Append(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_audits_and_returns_count_when_members_exist()
    {
        var adminId = Guid.NewGuid();
        var broadcaster = new Mock<Application.Contracts.Notifications.IMemberNotificationBroadcaster>();
        broadcaster.Setup(b => b.BroadcastToAllMembersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var audit = new Mock<IAdminAuditLogWriter>();
        var handler = new BroadcastMemberNotificationCommandHandler(broadcaster.Object, audit.Object);

        var result = await handler.Handle(
            new BroadcastMemberNotificationCommand(adminId, " Title ", " Body ", "/x", "10.0.0.1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecipientCount);
        audit.Verify(a => a.Append(
            adminId,
            AdminAuditActions.MemberNotificationBroadcast,
            null,
            "10.0.0.1",
            It.Is<string>(s => s.Contains("recipients=3", StringComparison.Ordinal) && s.Contains("Title", StringComparison.Ordinal))),
            Times.Once);
        broadcaster.Verify(b => b.BroadcastToAllMembersAsync("Title", "Body", "/x", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
