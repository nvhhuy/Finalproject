using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Contracts.Billing;

/// <summary>Deserialized from <see cref="Domains.Billing.Plan.LimitsJson"/>.</summary>
public sealed class PlanLimits
{
    public int MaxUsers { get; init; } = 5;
    public long MaxStorageBytes { get; init; } = 1_073_741_824;
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();
    public int MaxOpenSupportTickets { get; init; } = 20;
    public int AdminSeats { get; init; } = 1;
    public bool Impersonation { get; init; }
    public bool BackupRestore { get; init; }

    /// <summary>Per-tenant API requests allowed per minute (Phase 4.1).</summary>
    public int ApiRequestsPerMinute { get; init; } = 120;

    /// <summary>Personal wallet credits granted to the tenant owner each calendar month (Phase 4.6).</summary>
    public long MonthlyCreditGrant { get; init; }

    public static PlanLimits Default { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static PlanLimits Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;
        try
        {
            return JsonSerializer.Deserialize<PlanLimits>(json, JsonOptions) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public bool HasModule(string moduleCode) =>
        Modules.Any(m => string.Equals(m, moduleCode, StringComparison.OrdinalIgnoreCase));
}

public static class BillingModules
{
    public const string Support = "support";
    public const string Media = "media";
    public const string Notifications = "notifications";
}
