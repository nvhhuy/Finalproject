namespace Application.Contracts.Wallet;

/// <summary>Catalog of metered actions debited from a user's personal wallet (main app only).</summary>
public static class MeteredActions
{
    public const string ExportReport = "export_report";
    public const string PremiumJob = "premium_job";
    public const string BulkNotification = "bulk_notification";

    private static readonly IReadOnlyDictionary<string, long> Costs =
        new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            [ExportReport] = 50,
            [PremiumJob] = 100,
            [BulkNotification] = 25
        };

    public static bool TryGetCost(string actionCode, out long costCredits)
    {
        return Costs.TryGetValue(actionCode, out costCredits);
    }

    public static IReadOnlyList<MeteredActionDto> ListCatalog()
    {
        return Costs.Select(kv => new MeteredActionDto(kv.Key, kv.Value, Describe(kv.Key))).ToList();
    }

    private static string Describe(string code)
    {
        return code switch
        {
            ExportReport => "Export a detailed account report",
            PremiumJob => "Run a premium background job",
            BulkNotification => "Send a bulk notification batch",
            _ => code
        };
    }
}

public sealed record MeteredActionDto(string Code, long CostCredits, string Description);