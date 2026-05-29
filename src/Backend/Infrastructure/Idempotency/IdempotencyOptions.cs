namespace Infrastructure.Idempotency;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    /// <summary>How long completed responses are retained for replay.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>How long an in-progress claim blocks duplicate submits before expiring.</summary>
    public int InProgressTimeoutMinutes { get; set; } = 5;
}