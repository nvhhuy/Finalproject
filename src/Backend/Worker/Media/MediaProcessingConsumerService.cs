using Application.Contracts.Media;
using Application.Contracts.Messaging;

namespace Worker.Media;

/// <summary>
///     Drains the <c>media.uploaded.v1</c> queue and runs <see cref="IMediaProcessingPipeline" /> for
///     every message. The pipeline itself owns the asset lifecycle, retry decisions, and DB writes —
///     this class just adapts the RabbitMQ consumer signature to the pipeline call.
/// </summary>
/// <remarks>
///     Scope is created per message (via <see cref="IServiceScopeFactory" />) so each invocation gets a
///     fresh <c>ApplicationDbContext</c> / <c>IUnitOfWork</c> — critical because <see cref="IMediaProcessingPipeline" />
///     performs multiple <c>SaveChanges</c> within a single call. Without per-message scoping, change
///     tracking would leak across runs.
/// </remarks>
public sealed class MediaProcessingConsumerService : QueueConsumerBackgroundService<MediaUploadedMessage>
{
    /// <summary>
    ///     Must match <c>MediaAssetUploadedDomainEvent.RoutingKey</c>. Bound to the default topic exchange
    ///     1:1 so multiple worker instances share the same queue as competing consumers.
    /// </summary>
    public const string QueueName = "media.uploaded.v1";

    private readonly ILogger<MediaProcessingConsumerService> _logger;

    private readonly IServiceScopeFactory _scopeFactory;

    public MediaProcessingConsumerService(
        IMessageConsumer consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<MediaProcessingConsumerService> logger) : base(consumer, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override string SubscriptionName => QueueName;

    /// <summary>
    ///     Opt in to the broker's DLQ so a failing handler doesn't trigger nack-loops at RabbitMQ
    ///     level. Retry behaviour is handled by the application layer: <c>MediaAsset.MarkProcessing</c>
    ///     increments an attempt counter and the stale-recovery cron job re-emits the upload event
    ///     through the outbox (bounded by <c>WorkerSchedulingOptions.MaxMediaProcessingAttempts</c>).
    /// </summary>
    protected override SubscriptionOptions SubscriptionOptions { get; } = new(true);

    protected override async Task HandleAsync(MediaUploadedMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Media worker received {EventId} for asset {AssetId} ({Kind}, {Bytes:N0} B)",
            message.EventId,
            message.MediaAssetId,
            message.Kind,
            message.ByteSize);

        using var scope = _scopeFactory.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IMediaProcessingPipeline>();
        await pipeline.ProcessAsync(message.MediaAssetId, cancellationToken);
    }
}