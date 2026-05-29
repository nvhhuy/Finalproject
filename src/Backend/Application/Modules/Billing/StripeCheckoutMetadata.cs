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
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;
            if (!data.TryGetProperty("object", out var obj))
                return null;
            if (!obj.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
                return null;

            var userIdRaw = GetMetadata(metadata, "user_id", "userId");
            var planCode = GetMetadata(metadata, "plan_code", "planCode");
            var tenantSlug = GetMetadata(metadata, "tenant_slug", "tenantSlug");
            var tenantName = GetMetadata(metadata, "tenant_display_name", "tenantDisplayName");
            if (!Guid.TryParse(userIdRaw, out var userId) ||
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