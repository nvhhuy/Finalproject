namespace Application.Contracts.FeatureFlags;

/// <summary>
///     Evaluates feature flags with support for environment overrides, percentage rollout,
///     and database-backed toggles.
/// </summary>
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(
        string flagKey,
        FeatureFlagContext? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional targeting context for flag evaluation.</summary>
public sealed record FeatureFlagContext(
    Guid? UserId = null,
    string? TenantId = null);