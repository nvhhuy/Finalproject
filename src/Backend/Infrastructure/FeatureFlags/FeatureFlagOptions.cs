namespace Infrastructure.FeatureFlags;

public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";

    public Dictionary<string, FeatureFlagDefinition> Flags { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class FeatureFlagDefinition
{
    /// <summary>Default when no environment or rollout rule matches.</summary>
    public bool Default { get; set; } = true;

    /// <summary>Per-environment overrides keyed by ASP.NET environment name (Development, Staging, Production).</summary>
    public Dictionary<string, bool> Environments { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional percentage rollout (0–100). When set and <see cref="FeatureFlagContext.UserId"/> is present,
    /// a stable hash determines inclusion for canary releases.
    /// </summary>
    public int? Percentage { get; set; }

    /// <summary>When set, the flag also reads/writes the runtime <c>AppSettings</c> table via this key.</summary>
    public string? DbKey { get; set; }
}
