using Application.Contracts.Media;
using Application.Contracts.Messaging;
using Domains.MediaAggregate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Worker.Media;

namespace Worker.Tests.Media;

/// <summary>
///     Unit tests for <see cref="MediaProcessingConsumerService" />. The base class
///     <c>QueueConsumerBackgroundService</c> wires the consumer subscription; we capture the handler
///     delegate it registers and invoke it directly to assert pipeline dispatch.
/// </summary>
public sealed class MediaProcessingConsumerServiceTests
{
    [Fact]
    public async Task Handler_dispatches_to_pipeline_with_scoped_dbcontext_per_message()
    {
        var pipeline = new Mock<IMediaProcessingPipeline>();
        pipeline.Setup(p => p.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => pipeline.Object);
        IServiceProvider sp = services.BuildServiceProvider();

        Func<MediaUploadedMessage, CancellationToken, Task>? capturedHandler = null;
        var consumer = new Mock<IMessageConsumer>();
        consumer.Setup(c => c.SubscribeAsync(
                It.IsAny<string>(),
                It.IsAny<Func<MediaUploadedMessage, CancellationToken, Task>>(),
                It.IsAny<SubscriptionOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<MediaUploadedMessage, CancellationToken, Task>, SubscriptionOptions?,
                CancellationToken>((_, handler, _, _) => capturedHandler = handler)
            .ReturnsAsync(Mock.Of<IDisposable>());

        var svc = new MediaProcessingConsumerService(
            consumer.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MediaProcessingConsumerService>.Instance);

        // BackgroundService starts the subscription via ExecuteAsync; we trigger that by
        // calling StartAsync with a short-lived cancellation, then yielding so the subscribe
        // call is observed before we invoke the captured handler ourselves.
        using var cts = new CancellationTokenSource();
        var started = svc.StartAsync(cts.Token);
        // ExecuteAsync awaits Task.Delay(Infinite) so StartAsync returns once subscribe is wired.
        await started;

        Assert.NotNull(capturedHandler);

        var assetId = Guid.NewGuid();
        var message = new MediaUploadedMessage(
            Guid.NewGuid(),
            DateTime.UtcNow,
            assetId,
            Guid.NewGuid(),
            MediaKind.Image,
            "media/owner/asset/original",
            "image/jpeg",
            1234);

        await capturedHandler!(message, CancellationToken.None);

        pipeline.Verify(p => p.ProcessAsync(assetId, It.IsAny<CancellationToken>()), Times.Once);

        await svc.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handler_uses_dead_letter_subscription_so_failures_dont_thrash_the_broker()
    {
        var consumer = new Mock<IMessageConsumer>();
        SubscriptionOptions? captured = null;
        consumer.Setup(c => c.SubscribeAsync(
                It.IsAny<string>(),
                It.IsAny<Func<MediaUploadedMessage, CancellationToken, Task>>(),
                It.IsAny<SubscriptionOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Func<MediaUploadedMessage, CancellationToken, Task>, SubscriptionOptions?,
                CancellationToken>((_, _, options, _) => captured = options)
            .ReturnsAsync(Mock.Of<IDisposable>());

        var services = new ServiceCollection();
        IServiceProvider sp = services.BuildServiceProvider();

        var svc = new MediaProcessingConsumerService(
            consumer.Object,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MediaProcessingConsumerService>.Instance);

        await svc.StartAsync(CancellationToken.None);
        await svc.StopAsync(CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.EnableDeadLetterQueue,
            "Media consumer must opt in to the broker's DLQ so handler failures don't trigger "
            + "broker-level nack-loops — application-layer stale-recovery owns the retry.");
    }
}