namespace Infrastructure.Messaging.Outbox;

/// <summary>Tunables for <c>OutboxPublisherHostedService</c>. Bound from the <c>Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>When false the worker does not start (no draining). Useful for API-only deployments.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Idle delay between polling cycles when no rows are pending.</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>Maximum rows pulled per cycle.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Maximum publish attempts before the row is marked abandoned.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Initial backoff (seconds) after the first failure. Subsequent failures double the delay (capped at 1 hour).</summary>
    public int InitialBackoffSeconds { get; set; } = 10;
}