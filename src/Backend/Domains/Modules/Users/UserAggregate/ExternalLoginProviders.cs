namespace Domains.UserAggregate;

public static class ExternalLoginProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";

    public static string TenantOidc(Guid tenantId)
    {
        return $"oidc:{tenantId:N}";
    }
}