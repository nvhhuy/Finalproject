namespace Api.Configuration;

/// <summary>Feature-flag SignalR; enable when you add chat, live dashboards, or push notifications.</summary>
public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    /// <summary>When true, registers SignalR and maps <see cref="Hubs.NotificationHub" />.</summary>
    public bool Enabled { get; init; }

    /// <summary>When true and <c>Redis:ConnectionString</c> is set, uses Redis backplane for multi-instance scale-out.</summary>
    public bool UseRedisBackplane { get; init; } = true;
}