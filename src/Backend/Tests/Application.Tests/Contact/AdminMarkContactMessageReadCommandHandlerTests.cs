using Application.Contracts.Contact;
using Application.Modules.Contact;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Contact;

public sealed class AdminMarkContactMessageReadCommandHandlerTests
{
    [Fact]
    public async Task Handle_returns_not_found_when_service_reports_missing_message()
    {
        var service = new Mock<IContactMessageService>();
        service
            .Setup(s => s.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AdminMarkContactMessageReadCommandHandler(service.Object);

        var result = await handler.Handle(
            new AdminMarkContactMessageReadCommand(Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_true_when_service_marks_message_read()
    {
        var messageId = Guid.NewGuid();
        var service = new Mock<IContactMessageService>();
        service
            .Setup(s => s.MarkReadAsync(messageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new AdminMarkContactMessageReadCommandHandler(service.Object);

        var result = await handler.Handle(
            new AdminMarkContactMessageReadCommand(messageId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task Handle_passes_correct_message_id_to_service()
    {
        var messageId = Guid.NewGuid();
        var captured = Guid.Empty;

        var service = new Mock<IContactMessageService>();
        service
            .Setup(s => s.MarkReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) => captured = id)
            .ReturnsAsync(true);

        var handler = new AdminMarkContactMessageReadCommandHandler(service.Object);

        await handler.Handle(
            new AdminMarkContactMessageReadCommand(messageId),
            CancellationToken.None);

        Assert.Equal(messageId, captured);
    }
}
