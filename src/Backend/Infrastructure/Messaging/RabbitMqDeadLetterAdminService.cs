using System.Collections;
using System.Text;
using Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

/// <summary>
///     Reads / replays / drains a RabbitMQ dead-letter queue via basic.get. Uses its own short-lived
///     channels per operation so the long-running consumer's channel is unaffected (RabbitMQ channels
///     are not thread-safe).
/// </summary>
/// <remarks>
///     <para>
///         <b>Replay strategy:</b> we re-publish to the original primary queue (derived from the
///         <c>x-death</c> header set by the broker when it dead-lettered the message). If <c>x-death</c>
///         is missing or unparseable, we use the convention that DLQ queue names end in <c>.dlq</c> and
///         strip that suffix to recover the original — matches the layout declared by
///         <c>RabbitMqMessageConsumer.DeclareQueue</c>.
///     </para>
///     <para>
///         <b>Why basic.get instead of a consumer:</b> basic.get is synchronous — one round trip
///         returns either a message or null — which makes "drain up to N" trivially bounded. A
///         long-lived consumer would race against the live worker fleet for DLQ messages on the rare
///         queues where DLQ has its own consumer.
///     </para>
/// </remarks>
public sealed class RabbitMqDeadLetterAdminService : IDeadLetterAdminService
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqDeadLetterAdminService> _logger;
    private readonly RabbitMqOptions _options;

    public RabbitMqDeadLetterAdminService(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqDeadLetterAdminService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true
        };
        _connection = factory.CreateConnection();
    }

    public Task<IReadOnlyList<DeadLetterMessage>> PeekAsync(string queueName, int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName)) throw new ArgumentException("queueName required", nameof(queueName));
        var budget = Math.Clamp(maxMessages, 1, 500);

        using var channel = _connection.CreateModel();
        var results = new List<DeadLetterMessage>(budget);

        for (var i = 0; i < budget && !cancellationToken.IsCancellationRequested; i++)
        {
            var result = channel.BasicGet(queueName, false);
            if (result is null) break; // queue empty
            results.Add(MapToMessage(result));

            // Requeue so subsequent peeks see the same message. Without this, BasicGet+nack
            // would route to the DLQ's own DLQ (or none), losing visibility.
            channel.BasicNack(result.DeliveryTag, false, true);
        }

        return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(results);
    }

    public Task<int> ReplayAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName)) throw new ArgumentException("queueName required", nameof(queueName));
        var budget = Math.Clamp(maxMessages, 1, 500);

        using var channel = _connection.CreateModel();
        var replayed = 0;

        for (var i = 0; i < budget && !cancellationToken.IsCancellationRequested; i++)
        {
            var result = channel.BasicGet(queueName, false);
            if (result is null) break;

            var primaryQueue = InferPrimaryQueueName(queueName, result.BasicProperties);
            // Republish via the default exchange (empty string) routed by queue name — same
            // convention RabbitMqMessageConsumer.DeclareQueue uses when binding the primary.
            try
            {
                var newProps = CopyProperties(channel, result.BasicProperties);
                channel.BasicPublish(_options.DefaultExchange, primaryQueue, newProps, result.Body);
                channel.BasicAck(result.DeliveryTag, false);
                replayed++;
                _logger.LogInformation(
                    "DLQ replay {Index}/{Budget}: {Dlq} → {Primary} (correlation={Correlation})",
                    i + 1, budget, queueName, primaryQueue, newProps.CorrelationId);
            }
            catch (Exception ex)
            {
                // Don't lose the DLQ copy if the republish failed — requeue so the operator can
                // retry once the underlying broker issue is fixed.
                _logger.LogError(ex, "Replay failed for DLQ message on {Queue}; requeuing", queueName);
                channel.BasicNack(result.DeliveryTag, false, true);
                break;
            }
        }

        return Task.FromResult(replayed);
    }

    public Task<int> DrainAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queueName)) throw new ArgumentException("queueName required", nameof(queueName));
        var budget = Math.Clamp(maxMessages, 1, 500);

        using var channel = _connection.CreateModel();
        var drained = 0;

        for (var i = 0; i < budget && !cancellationToken.IsCancellationRequested; i++)
        {
            var result = channel.BasicGet(queueName, false);
            if (result is null) break;
            channel.BasicAck(result.DeliveryTag, false);
            drained++;
            _logger.LogWarning(
                "DLQ drain {Index}/{Budget}: {Queue} discarded message (delivery-tag={Tag}, correlation={Correlation})",
                i + 1, budget, queueName, result.DeliveryTag, result.BasicProperties?.CorrelationId);
        }

        return Task.FromResult(drained);
    }

    private static DeadLetterMessage MapToMessage(BasicGetResult result)
    {
        var preview = Encoding.UTF8.GetString(result.Body.Span);
        if (preview.Length > 512) preview = preview[..512] + "…";

        string? originalRoutingKey = null;
        string? originalExchange = null;
        var retryCount = 0;
        string? lastReason = null;

        // x-death is the broker-emitted history of dead-letter routings. It's an AMQP array of
        // tables; each table has keys like queue/reason/count/exchange/routing-keys. We read
        // the most-recent (first) entry.
        if (result.BasicProperties?.Headers is { } headers
            && headers.TryGetValue("x-death", out var raw)
            && raw is IList list
            && list.Count > 0
            && list[0] is IDictionary<string, object> first)
        {
            if (first.TryGetValue("count", out var cnt) && cnt is long cntL) retryCount = (int)cntL;
            if (first.TryGetValue("reason", out var r) && r is byte[] rb) lastReason = Encoding.UTF8.GetString(rb);
            if (first.TryGetValue("exchange", out var ex) && ex is byte[] exb)
                originalExchange = Encoding.UTF8.GetString(exb);
            if (first.TryGetValue("routing-keys", out var rk) && rk is IList rkList && rkList.Count > 0 &&
                rkList[0] is byte[] rkb)
                originalRoutingKey = Encoding.UTF8.GetString(rkb);
        }

        return new DeadLetterMessage(
            result.DeliveryTag,
            originalRoutingKey,
            originalExchange,
            result.BasicProperties?.CorrelationId,
            retryCount,
            lastReason,
            preview);
    }

    private static string InferPrimaryQueueName(string dlqName, IBasicProperties? properties)
    {
        // Prefer the broker-recorded original queue from x-death; fall back to the suffix convention.
        if (properties?.Headers is { } headers
            && headers.TryGetValue("x-death", out var raw)
            && raw is IList list
            && list.Count > 0
            && list[0] is IDictionary<string, object> first
            && first.TryGetValue("queue", out var q)
            && q is byte[] qb)
            return Encoding.UTF8.GetString(qb);

        // Convention used by RabbitMqMessageConsumer.DeclareQueue: primary `foo` → DLQ `foo.dlq`.
        const string suffix = ".dlq";
        return dlqName.EndsWith(suffix, StringComparison.Ordinal)
            ? dlqName[..^suffix.Length]
            : dlqName;
    }

    private static IBasicProperties CopyProperties(IModel channel, IBasicProperties? source)
    {
        var dest = channel.CreateBasicProperties();
        if (source is null) return dest;
        if (!string.IsNullOrEmpty(source.CorrelationId)) dest.CorrelationId = source.CorrelationId;
        if (!string.IsNullOrEmpty(source.MessageId)) dest.MessageId = source.MessageId;
        if (!string.IsNullOrEmpty(source.ContentType)) dest.ContentType = source.ContentType;
        if (!string.IsNullOrEmpty(source.Type)) dest.Type = source.Type;
        // Intentionally do NOT copy the x-death header — the broker re-stamps it on the next
        // dead-letter, and copying the old history would compound retry counts misleadingly.
        if (source.Headers is { } h)
        {
            // IBasicProperties.Headers is IDictionary<string, object?> in RabbitMQ.Client 6.x —
            // iterate as KVP rather than legacy DictionaryEntry so the type matches.
            dest.Headers = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> entry in h)
                if (!string.Equals(entry.Key, "x-death", StringComparison.Ordinal))
                    dest.Headers[entry.Key] = entry.Value!;
        }

        return dest;
    }
}