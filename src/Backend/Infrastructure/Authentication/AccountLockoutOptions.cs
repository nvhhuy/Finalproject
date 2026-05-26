namespace Infrastructure.Authentication;

/// <summary>Tunes the failed-login lockout policy applied by <see cref="CacheFailedLoginTracker"/>.</summary>
public sealed class AccountLockoutOptions
{
    public const string SectionName = "Authentication:Lockout";

    /// <summary>Failures allowed inside <see cref="WindowSeconds"/> before a lockout is triggered.</summary>
    public int MaxFailures { get; set; } = 10;

    /// <summary>Rolling window (seconds) during which failures accumulate.</summary>
    public int WindowSeconds { get; set; } = 900;

    /// <summary>How long the identity is locked once the threshold is hit (seconds).</summary>
    public int LockoutSeconds { get; set; } = 900;
}
