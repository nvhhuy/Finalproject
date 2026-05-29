using System.Text.Json;

namespace Application.Modules.Billing;

public sealed record StripeCheckoutMetadata(
    Guid UserId,
    string PlanCode,
    string TenantSlug,
    string TenantDisplayName,
    string? PublicTagline,
    string? PublicDescription)
{
    public static StripeCheckoutMetadata? TryParseFromStripePayload(string payloadJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("data", out JsonElement data))
                return null;
            if (!data.TryGetProperty("object", out JsonElement obj))
                return null;
            if (!obj.TryGetProperty("metadata", out JsonElement metadata) || metadata.ValueKind != JsonValueKind.Object)
                return null;

            string? userIdRaw = GetMetadata(metadata, "user_id", "userId");
            string? planCode = GetMetadata(metadata, "plan_code", "planCode");
            string? tenantSlug = GetMetadata(metadata, "tenant_slug", "tenantSlug");
            string? tenantName = GetMetadata(metadata, "tenant_display_name", "tenantDisplayName");
            if (!Guid.TryParse(userIdRaw, out Guid userId) ||
                string.IsNullOrWhiteSpace(planCode) ||
                string.IsNullOrWhiteSpace(tenantSlug) ||
                string.IsNullOrWhiteSpace(tenantName))
                return null;

            return new StripeCheckoutMetadata(
                userId,
                planCode,
                tenantSlug,
                tenantName,
                GetMetadata(metadata, "public_tagline", "publicTagline"),
                GetMetadata(metadata, "public_description", "publicDescription"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetMetadata(JsonElement metadata, params string[] keys)
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
