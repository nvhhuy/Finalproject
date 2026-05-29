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
            using JsonDocument doc = JsonDocument.Parse(payloadJson);
            JsonElement root = doc.RootElement;

            if (TryParseFromMetadata(root, out WalletPurchaseWebhookMetadata? fromRoot))
                return fromRoot;

            if (root.TryGetProperty("data", out JsonElement data) &&
                data.TryGetProperty("object", out JsonElement stripeObj) &&
                TryParseFromMetadata(stripeObj, out WalletPurchaseWebhookMetadata? fromStripe))
                return fromStripe;

            if (root.TryGetProperty("metadata", out JsonElement metadata) &&
                TryParseMetadataObject(metadata, out WalletPurchaseWebhookMetadata? fromMeta))
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
        if (container.TryGetProperty("metadata", out JsonElement metadata))
            return TryParseMetadataObject(metadata, out result);
        return TryParseMetadataObject(container, out result);
    }

    private static bool TryParseMetadataObject(JsonElement metadata, out WalletPurchaseWebhookMetadata? result)
    {
        result = null;
        if (metadata.ValueKind != JsonValueKind.Object)
            return false;

        string? purpose = GetString(metadata, "purpose");
        if (!string.Equals(purpose, PurposeValue, StringComparison.OrdinalIgnoreCase))
            return false;

        string? userIdRaw = GetString(metadata, "user_id", "userId");
        string? packageCode = GetString(metadata, "package_code", "packageCode");
        if (!Guid.TryParse(userIdRaw, out Guid userId) || string.IsNullOrWhiteSpace(packageCode))
            return false;

        result = new WalletPurchaseWebhookMetadata(userId, CreditPackage.NormalizeCode(packageCode));
        return true;
    }

    private static string? GetString(JsonElement metadata, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (metadata.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? s = value.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        return null;
    }
}
