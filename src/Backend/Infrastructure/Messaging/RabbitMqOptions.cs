namespace Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "Messaging:RabbitMq";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    /// <summary>Default exchange for publishes when <see cref="MessagePublishContext.TopicOrExchange"/> is empty.</summary>
    public string DefaultExchange { get; set; } = "finalproject.events";

    /// <summary>
    /// Per-channel prefetch count. Without this, RabbitMQ floods one consumer with the whole
    /// queue and bypasses competing-consumer load balancing entirely. 16 is a conservative
    /// default for media workers (each message can spawn multi-minute ffmpeg work) — raise for
    /// cheap message handlers, lower for memory-bound ones.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>
    /// How long the publisher waits for the broker's confirm.select acknowledgement before
    /// treating the publish as failed. Outbox messages without a confirm stay <c>pending</c>
    /// and retry on the next tick.
    /// </summary>
    public int PublishConfirmTimeoutSeconds { get; set; } = 5;
}
