namespace Application.Contracts.Tenancy;

public sealed class TenancyHostOptions
{
    public const string SectionName = "Tenancy";

    /// <summary>Base host for tenant subdomains, e.g. <c>app.example.com</c> → <c>{slug}.app.example.com</c>.</summary>
    public string? SubdomainBaseHost { get; set; }

    /// <summary>Subdomains reserved for platform routes (not tenant slugs).</summary>
    public string[] ReservedSubdomains { get; set; } = ["www", "api", "admin", "app", "mail", "static"];

    /// <summary>TXT record prefix for domain verification, e.g. <c>_baseproject-verify</c>.</summary>
    public string VerificationTxtPrefix { get; set; } = "_baseproject-verify";

    /// <summary>Expected CNAME target shown to customers, e.g. <c>tenants.app.example.com</c>.</summary>
    public string? PlatformCnameTarget { get; set; }
}
