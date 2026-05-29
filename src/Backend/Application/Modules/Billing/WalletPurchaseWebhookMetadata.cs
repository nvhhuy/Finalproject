using System.Text.Json;
using Domains.Billing;

namespace Application.Modules.Billing;

/// <summary>Parses wallet top-up metadata from payment provider webhook payloads.</summary>
public sealed record WalletPurchaseWebhookMetadata(Guid UserId, string PackageCode)
{
    public const string PurposeValue = "wallet_topup";

    public static WalletPurchaseWebhookMetadata? TryParse(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (TryParseFromMetadata(root, out var fromRoot))
                return fromRoot;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("object", out var stripeObj) &&
                TryParseFromMetadata(stripeObj, out var fromStripe))
                return fromStripe;

            if (root.TryGetProperty("metadata", out var metadata) &&
                TryParseMetadataObject(metadata, out var fromMeta))
                return fromMeta;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryParseFromMetadata(JsonElement container, out WalletPurchaseWebhookMetadata? result)
    {
        result = null;
        if (container.TryGetProperty("metadata", out var metadata))
            return TryParseMetadataObject(metadata, out result);
        return TryParseMetadataObject(container, out result);
    }

    private static bool TryParseMetadataObject(JsonElement metadata, out WalletPurchaseWebhookMetadata? result)
    {
        result = null;
        if (metadata.ValueKind != JsonValueKind.Object)
            return false;

        var purpose = GetString(metadata, "purpose");
        if (!string.Equals(purpose, PurposeValue, StringComparison.OrdinalIgnoreCase))
            return false;

        var userIdRaw = GetString(metadata, "user_id", "userId");
        var packageCode = GetString(metadata, "package_code", "packageCode");
        if (!Guid.TryParse(userIdRaw, out var userId) || string.IsNullOrWhiteSpace(packageCode))
            return false;

        result = new WalletPurchaseWebhookMetadata(userId, CreditPackage.NormalizeCode(packageCode));
        return true;
    }

    private static string? GetString(JsonElement metadata, params string[] keys)
    {
        foreach (var key in keys)
            if (metadata.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var s = value.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }

        return null;
    }
}