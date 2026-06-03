using Application.Contracts.Notifications;
using Application.Contracts.Messaging;

namespace Worker.Notifications;

public sealed class MemberNotificationBroadcastConsumerService
    : QueueConsumerBackgroundService<MemberNotificationBroadcastMessage>
{
    public const string QueueName = "notifications.broadcast.requested.v1";

    private readonly ILogger<MemberNotificationBroadcastConsumerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public MemberNotificationBroadcastConsumerService(
        IMessageConsumer consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<MemberNotificationBroadcastConsumerService> logger)
        : base(consumer, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override string SubscriptionName => QueueName;

    protected override SubscriptionOptions SubscriptionOptions { get; } = new(true);

    protected override async Task HandleAsync(
        MemberNotificationBroadcastMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Notification broadcast worker fan-out for event {EventId} ({Title})",
            message.EventId,
            message.Title);

        using var scope = _scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IMemberNotificationBroadcastProcessor>();
        var inserted = await processor.ProcessAsync(
            new MemberNotificationBroadcastPayload(
                message.EventId,
                message.Title,
                message.Body,
                message.LinkUrl),
            cancellationToken);

        _logger.LogInformation(
            "Notification broadcast {EventId} inserted {Count} row(s).",
            message.EventId,
            inserted);
    }
}
