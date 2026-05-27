using Application.Contracts.FeatureFlags;
using Application.Contracts.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.FeatureFlags;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagOptions _options;
    private readonly IAppSettingsService _appSettings;
    private readonly IHostEnvironment _environment;

    public FeatureFlagService(
        IOptions<FeatureFlagOptions> options,
        IAppSettingsService appSettings,
        IHostEnvironment environment)
    {
        _options = options.Value;
        _appSettings = appSettings;
        _environment = environment;
    }

    public async Task<bool> IsEnabledAsync(
        string flagKey,
        FeatureFlagContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Flags.TryGetValue(flagKey, out FeatureFlagDefinition? definition))
            return true;

        if (!string.IsNullOrWhiteSpace(definition.DbKey))
        {
            bool dbValue = await _appSettings.GetBooleanAsync(definition.DbKey, definition.Default, cancellationToken);
            if (!dbValue)
                return false;
        }

        if (definition.Environments.TryGetValue(_environment.EnvironmentName, out bool envValue))
            return envValue;

        if (definition.Percentage is int pct && context?.UserId is Guid userId)
            return IsInPercentageRollout(flagKey, userId, context.TenantId, pct);

        return definition.Default;
    }

    private static bool IsInPercentageRollout(string flagKey, Guid userId, string? tenantId, int percentage)
    {
        percentage = Math.Clamp(percentage, 0, 100);
        if (percentage >= 100)
            return true;
        if (percentage <= 0)
            return false;

        string seed = $"{flagKey}:{tenantId ?? "global"}:{userId:N}";
        int bucket = Math.Abs(seed.GetHashCode(StringComparison.Ordinal)) % 100;
        return bucket < percentage;
    }
}
