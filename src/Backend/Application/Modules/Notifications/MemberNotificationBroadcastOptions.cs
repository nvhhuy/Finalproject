namespace Application.Modules.Notifications;

public sealed class MemberNotificationBroadcastOptions
{
    public const string SectionName = "Notifications:Broadcast";

    /// <summary>Users loaded and inserted per batch during fan-out.</summary>
    public int InsertBatchSize { get; init; } = 500;

    /// <summary>
    ///     When true (integration tests), fan-out runs inline after the outbox row is written so
    ///     assertions do not depend on RabbitMQ or a running Worker.
    /// </summary>
    public bool ProcessInlineInIntegrationTests { get; init; }
}
