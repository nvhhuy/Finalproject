namespace Domains.Tenancy;

/// <summary>Well-known tenant identifiers used for migration backfill and system defaults.</summary>
public static class SystemTenants
{
    public const string DefaultSlug = "default";
    public const string DefaultDisplayName = "Default Organization";
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}