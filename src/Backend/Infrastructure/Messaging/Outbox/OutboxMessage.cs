namespace Infrastructure.Messaging.Outbox;

/// <summary>
/// Persistent record of an integration event awaiting publish to RabbitMQ. Written inside the
/// same transaction as the business state so the producer side is atomic; a background worker
/// (<c>OutboxPublisherHostedService</c>) drains rows and publishes to the broker with at-least-once
/// semantics. Consumers must be idempotent on <see cref="EventId"/>.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(
        Guid eventId,
        DateTime occurredOnUtc,
        string type,
        string routingKey,
        string? exchange,
        string payloadJson)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        RoutingKey = routingKey;
        Exchange = exchange;
        PayloadJson = payloadJson;
    }

    /// <summary>Surrogate primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Stable event id assigned by the producing aggregate (used for idempotency downstream).</summary>
    public Guid EventId { get; private set; }

    public DateTime OccurredOnUtc { get; private set; }

    /// <summary>CLR type name (assembly-qualified) of the integration event. Stored for traceability.</summary>
    public string Type { get; private set; } = null!;

    /// <summary>AMQP routing key.</summary>
    public string RoutingKey { get; private set; } = null!;

    /// <summary>Optional exchange override. <c>null</c> → broker default exchange (from RabbitMqOptions).</summary>
    public string? Exchange { get; private set; }

    /// <summary>UTF-8 JSON payload published to the broker as-is.</summary>
    public string PayloadJson { get; private set; } = null!;

    /// <summary>When the worker successfully published the row. <c>null</c> while pending.</summary>
    public DateTime? ProcessedOnUtc { get; private set; }

    /// <summary>Number of publish attempts (including the successful one).</summary>
    public int Attempts { get; private set; }

    /// <summary>Earliest UTC at which the worker may retry. <c>null</c> means eligible immediately.</summary>
    public DateTime? NextAttemptUtc { get; private set; }

    /// <summary>Last error message captured by the worker (truncated).</summary>
    public string? Error { get; private set; }

    public void MarkProcessed(DateTime utcNow)
    {
        ProcessedOnUtc = utcNow;
        Attempts += 1;
        Error = null;
        NextAttemptUtc = null;
    }

    public void RecordFailure(DateTime utcNow, string error, TimeSpan nextDelay)
    {
        Attempts += 1;
        Error = error.Length <= 2000 ? error : error[..2000];
        NextAttemptUtc = utcNow + nextDelay;
    }

    /// <summary>Stops further retries (e.g. after exceeding the attempt cap).</summary>
    public void MarkAbandoned(DateTime utcNow, string error)
    {
        Attempts += 1;
        Error = error.Length <= 2000 ? error : error[..2000];
        ProcessedOnUtc = utcNow;
        NextAttemptUtc = null;
    }
}
