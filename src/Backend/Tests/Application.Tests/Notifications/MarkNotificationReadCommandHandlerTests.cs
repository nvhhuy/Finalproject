using Application.Contracts.Notifications;
using Application.Modules.Notifications.Commands;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Notifications;

public sealed class MarkNotificationReadCommandHandlerTests
{
    [Fact]
    public async Task Handle_returns_not_found_when_writer_reports_missing_row()
    {
        var writer = new Mock<IUserNotificationWriter>();
        writer
            .Setup(w => w.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new MarkNotificationReadCommandHandler(writer.Object);
        var result = await handler.Handle(
            new MarkNotificationReadCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_true_when_notification_is_marked_read()
    {
        var userId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();

        var writer = new Mock<IUserNotificationWriter>();
        writer
            .Setup(w => w.MarkReadAsync(userId, notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new MarkNotificationReadCommandHandler(writer.Object);
        var result = await handler.Handle(
            new MarkNotificationReadCommand(userId, notificationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
}