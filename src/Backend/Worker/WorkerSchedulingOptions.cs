namespace Worker;

/// <summary>Worker-only scheduling knobs (cron expressions are standard 5-field UTC).</summary>
public sealed class WorkerSchedulingOptions
{
    public const string SectionName = "Worker";

    /// <summary>Cron for <see cref="SampleCronJob"/>; default every 10 minutes.</summary>
    public string SampleCronExpression { get; init; } = "*/10 * * * *";

    /// <summary>When true, registers <see cref="SampleBackgroundService"/> (long-running loop template). Off by default for production.</summary>
    public bool EnableSampleBackgroundLoop { get; init; }
}
