using Application.Contracts.Contact;
using Application.Modules.Contact;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Contact;

public sealed class SubmitContactMessageCommandHandlerTests
{
    private static SubmitContactMessageCommandHandler BuildHandler(IContactMessageService? service = null)
    {
        var mock = service ?? Mock.Of<IContactMessageService>();
        return new SubmitContactMessageCommandHandler(mock);
    }

    [Theory]
    [InlineData("", "jane@example.com", "Subject", "Body")]
    [InlineData("Jane", "", "Subject", "Body")]
    [InlineData("Jane", "jane@example.com", "", "Body")]
    [InlineData("Jane", "jane@example.com", "Subject", "")]
    public async Task Handle_returns_bad_request_when_any_field_is_empty(
        string name, string email, string subject, string body)
    {
        var handler = BuildHandler();

        var result = await handler.Handle(
            new SubmitContactMessageCommand(name, email, subject, body),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error.StatusCode);
    }

    [Theory]
    [InlineData("   ", "jane@example.com", "Subject", "Body")]
    [InlineData("Jane", "jane@example.com", "  ", "Body")]
    [InlineData("Jane", "jane@example.com", "Subject", "   ")]
    public async Task Handle_returns_bad_request_when_any_field_is_whitespace_only(
        string name, string email, string subject, string body)
    {
        var handler = BuildHandler();

        var result = await handler.Handle(
            new SubmitContactMessageCommand(name, email, subject, body),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error.StatusCode);
    }

    [Fact]
    public async Task Handle_delegates_to_service_and_returns_new_id()
    {
        var expectedId = Guid.NewGuid();
        var service = new Mock<IContactMessageService>();
        service
            .Setup(s => s.SubmitAsync("Jane Smith", "jane@example.com", "Billing", "Help please.",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        var handler = BuildHandler(service.Object);

        var result = await handler.Handle(
            new SubmitContactMessageCommand("Jane Smith", "jane@example.com", "Billing", "Help please."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedId, result.Value);
    }

    [Fact]
    public async Task Handle_does_not_call_service_when_validation_fails()
    {
        var service = new Mock<IContactMessageService>();

        var handler = BuildHandler(service.Object);

        await handler.Handle(
            new SubmitContactMessageCommand("", "jane@example.com", "Subject", "Body"),
            CancellationToken.None);

        service.Verify(
            s => s.SubmitAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
