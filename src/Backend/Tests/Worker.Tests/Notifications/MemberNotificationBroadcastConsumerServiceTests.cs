using Application.Contracts.Notifications;
using Application.Contracts.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Worker.Notifications;

namespace Worker.Tests.Notifications;

public sealed class MemberNotificationBroadcastConsumerServiceTests
{
    [Fact]
    public async Task Handler_dispatches_to_broadcast_processor()
    {
        var processor = new Mock<IMemberNotificationBroadcastProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<MemberNotificationBroadcastPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var services = new ServiceCollection();
        services.AddScoped(_ => processor.Object);
        IServiceProvider sp = services.BuildServiceProvider();

        Func<MemberNotificationBroadcastMessage, CancellationToken, Task>? capturedHandler = null;
        var consumer = new Mock<IMessageConsumer>();
        consumer.Setup(c => c.SubscribeAsync(
                It.IsAny<string>(),
                It.IsAny<Func<MemberNotificationBroadcastMessage, CancellationToken, Task>>(),
                It.IsAny<SubscriptionOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<MemberNotificationBroadcastMessage, CancellationToken, Task>, SubscriptionOptions?,
                CancellationToken>((_, handler, _, _) => capturedHandler = handler)
            .ReturnsAsync(Mock.Of<IDisposable>());

        var svc = new MemberNotificationBroadcastConsumerService(
            consumer.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemberNotificationBroadcastConsumerService>.Instance);

        using var cts = new CancellationTokenSource();
        _ = svc.StartAsync(cts.Token);
        await Task.Yield();
        Assert.NotNull(capturedHandler);

        var message = new MemberNotificationBroadcastMessage(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Title",
            "Body",
            "/app");
        await capturedHandler!(message, CancellationToken.None);

        processor.Verify(
            p => p.ProcessAsync(
                It.Is<MemberNotificationBroadcastPayload>(x =>
                    x.EventId == message.EventId && x.Title == "Title" && x.Body == "Body" && x.LinkUrl == "/app"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await svc.StopAsync(CancellationToken.None);
    }
}
