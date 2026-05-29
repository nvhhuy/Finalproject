namespace Worker;

/// <summary>Worker-only scheduling knobs (cron expressions are standard 5-field UTC).</summary>
public sealed class WorkerSchedulingOptions
{
    public const string SectionName = "Worker";

    /// <summary>Cron for <see cref="SampleCronJob"/>; default every 10 minutes.</summary>
    public string SampleCronExpression { get; init; } = "*/10 * * * *";

    /// <summary>When true, registers <see cref="SampleBackgroundService"/> (long-running loop template). Off by default for production.</summary>
    public bool EnableSampleBackgroundLoop { get; init; }

    /// <summary>
    /// Cron for <see cref="Worker.Media.StaleMediaRecoveryJob"/>; default every 5 minutes. Tight
    /// interval is cheap because the query is indexed and bounded by <see cref="StaleMediaRecoveryBatchSize"/>.
    /// </summary>
    public string StaleMediaRecoveryCronExpression { get; init; } = "*/5 * * * *";

    /// <summary>
    /// How long an asset can remain in <c>Processing</c> before the recovery job considers it
    /// wedged and requeues it. Default 30 minutes — long enough for a 4K transcode on modest
    /// hardware, short enough that a crashed worker doesn't strand uploads for hours.
    /// </summary>
    public int StaleMediaProcessingThresholdMinutes { get; init; } = 30;

    /// <summary>Max stalled assets requeued per tick. Bounds the impact of a large backlog post-outage.</summary>
    public int StaleMediaRecoveryBatchSize { get; init; } = 50;

    /// <summary>
    /// Maximum number of processing attempts before a stalled asset is permanently parked in
    /// <c>Failed</c> status. Default 5 — generous enough that a single transient worker crash
    /// (OOM after a brief memory spike) doesn't dead-letter a legitimate upload, tight enough that
    /// a corrupt input that consistently kills the worker doesn't burn unlimited CPU.
    /// </summary>
    public int MaxMediaProcessingAttempts { get; init; } = 5;

    /// <summary>
    /// Cron for <see cref="Worker.Media.MediaStorageCleanupJob"/>; default daily at 03:00 UTC.
    /// Cleanup is a high-cost scan (lists the whole <c>media/</c> prefix) so it should not run
    /// during peak traffic.
    /// </summary>
    public string MediaStorageCleanupCronExpression { get; init; } = "0 3 * * *";

    /// <summary>
    /// Minimum age (hours) of a storage object before the cleanup job will consider it orphaned.
    /// Default 24h — long enough that an in-flight upload (presigned URL valid for 10 minutes,
    /// but client may retry for hours) doesn't get garbage-collected mid-flight.
    /// </summary>
    public int MediaStorageOrphanMinAgeHours { get; init; } = 24;

    /// <summary>Max objects deleted per cleanup run. Bounds the operational blast radius.</summary>
    public int MediaStorageCleanupBatchSize { get; init; } = 500;

    /// <summary>Enables the cleanup job. Off by default — operators opt in once they've reviewed the layout in <c>MediaKeyPlanner</c>.</summary>
    public bool EnableMediaStorageCleanup { get; init; }

    /// <summary>
    /// Cron for <see cref="Worker.Outbox.OutboxCleanupJob"/>; default daily at 02:30 UTC. Deletes
    /// fully-processed outbox rows older than <see cref="OutboxRetentionDays"/> so the table
    /// stays bounded over time.
    /// </summary>
    public string OutboxCleanupCronExpression { get; init; } = "30 2 * * *";

    /// <summary>
    /// How many days to keep <c>ProcessedOnUtc</c>-marked rows for audit/replay before deletion.
    /// Default 30 — long enough to investigate stale incidents, short enough to keep the table
    /// from accumulating gigabytes over months of steady traffic.
    /// </summary>
    public int OutboxRetentionDays { get; init; } = 30;

    /// <summary>
    /// How many days to keep <c>AbandonedAtUtc</c>-marked rows. Default 90 — abandoned rows
    /// represent local dead-letters that an operator may want to inspect and replay before they
    /// disappear, so the retention is significantly longer than the success retention.
    /// </summary>
    public int OutboxAbandonedRetentionDays { get; init; } = 90;

    /// <summary>Max rows deleted per cleanup tick. Bounds lock duration on large backlogs.</summary>
    public int OutboxCleanupBatchSize { get; init; } = 5_000;

    /// <summary>
    /// Cron for system-settings backup snapshots; default daily at 01:15 UTC so it runs before
    /// most heavy maintenance windows and leaves room for retries.
    /// </summary>
    public string SystemSettingsBackupCronExpression { get; init; } = "15 1 * * *";

    /// <summary>
    /// Cron for full PostgreSQL logical backups; default daily at 01:45 UTC.
    /// </summary>
    public string DatabaseBackupCronExpression { get; init; } = "45 1 * * *";

    /// <summary>Cron for <see cref="Worker.Billing.SubscriptionDunningJob"/>; default hourly.</summary>
    public string SubscriptionDunningCronExpression { get; init; } = "0 * * * *";

    /// <summary>Cron for <see cref="Worker.Billing.SubscriptionCreditGrantJob"/>; default daily at 00:30 UTC.</summary>
    public string SubscriptionCreditGrantCronExpression { get; init; } = "30 0 * * *";

    /// <summary>Cron for <see cref="Worker.Notifications.NotificationDeliveryJob"/>; default every minute.</summary>
    public string NotificationDeliveryCronExpression { get; init; } = "* * * * *";

    public int NotificationDeliveryBatchSize { get; init; } = 200;
}
