namespace Application.Contracts.Messaging;

/// <summary>
///     Operator tooling for inspecting and triaging dead-letter queues. The default consumer flow
///     nacks-without-requeue on handler failure (so the message goes to <c>{queue}.dlq</c>) — this
///     service is how on-call gets those messages back into the primary queue once the underlying
///     bug is fixed.
/// </summary>
/// <remarks>
///     <para>
///         <b>Authorisation:</b> implementations expose no auth themselves; the HTTP endpoint that
///         surfaces them must be admin-gated. Replaying messages can re-trigger external side effects
///         (emails, charges) — wrong hands here is a real incident.
///     </para>
///     <para>
///         <b>Semantics:</b> all operations use AMQP's basic.get loop (not a long-lived consumer),
///         so they're safe to call concurrently with the regular consumer fleet. Each call pulls up to
///         <c>maxMessages</c> and stops when the queue is empty.
///     </para>
/// </remarks>
public interface IDeadLetterAdminService
{
    /// <summary>
    ///     Fetches up to <paramref name="maxMessages" /> from <paramref name="queueName" /> without
    ///     removing them. Used by operators to triage what's accumulated in a DLQ before deciding
    ///     to replay or drain.
    /// </summary>
    Task<IReadOnlyList<DeadLetterMessage>> PeekAsync(string queueName, int maxMessages,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pulls up to <paramref name="maxMessages" /> from <paramref name="queueName" /> and
    ///     republishes each to the original primary queue, then acks the DLQ copy. Returns the
    ///     number of messages successfully replayed.
    /// </summary>
    Task<int> ReplayAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pulls up to <paramref name="maxMessages" /> from <paramref name="queueName" /> and acks
    ///     them without republishing — effectively discarding. Returns the number drained.
    /// </summary>
    Task<int> DrainAsync(string queueName, int maxMessages, CancellationToken cancellationToken = default);
}

/// <summary>
///     One DLQ entry surfaced by <see cref="IDeadLetterAdminService.PeekAsync" />.
/// </summary>
/// <param name="DeliveryTag">Broker-side identifier for the fetched message (opaque to callers).</param>
/// <param name="OriginalRoutingKey">
///     Routing key the message was originally published with (derived from <c>x-death</c>
///     header when present).
/// </param>
/// <param name="OriginalExchange">Exchange the message was originally published to.</param>
/// <param name="CorrelationId">Correlation id stamped at publish time, if any.</param>
/// <param name="RetryCount">Times the broker has dead-lettered this message (from <c>x-death</c> count).</param>
/// <param name="LastFailureReason">Why the message was dead-lettered (rejected | expired | maxlen).</param>
/// <param name="PayloadPreview">First 512 bytes of the payload as UTF-8 text (truncated with an ellipsis when longer).</param>
public sealed record DeadLetterMessage(
    ulong DeliveryTag,
    string? OriginalRoutingKey,
    string? OriginalExchange,
    string? CorrelationId,
    int RetryCount,
    string? LastFailureReason,
    string PayloadPreview);