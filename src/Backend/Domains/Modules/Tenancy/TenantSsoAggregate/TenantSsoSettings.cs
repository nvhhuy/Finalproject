using Domains.Common;
using Domains.Tenancy.ValueObjects;

namespace Domains.Tenancy;

public sealed class TenantSsoSettings : Entity<Guid>
{
    public TenantId TenantId { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public string OidcAuthority { get; private set; } = null!;
    public string OidcClientId { get; private set; } = null!;
    public string? OidcClientSecret { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private TenantSsoSettings() { }

    private TenantSsoSettings(
        Guid id,
        TenantId tenantId,
        bool isEnabled,
        string oidcAuthority,
        string oidcClientId,
        string? oidcClientSecret,
        DateTime utcNow) : base(id)
    {
        TenantId = tenantId;
        IsEnabled = isEnabled;
        OidcAuthority = oidcAuthority;
        OidcClientId = oidcClientId;
        OidcClientSecret = oidcClientSecret;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public static TenantSsoSettings Create(
        TenantId tenantId,
        bool isEnabled,
        string oidcAuthority,
        string oidcClientId,
        string? oidcClientSecret,
        DateTime utcNow) =>
        new(
            Guid.NewGuid(),
            tenantId,
            isEnabled,
            NormalizeAuthority(oidcAuthority),
            oidcClientId.Trim(),
            string.IsNullOrWhiteSpace(oidcClientSecret) ? null : oidcClientSecret.Trim(),
            utcNow);

    public void Update(
        bool isEnabled,
        string oidcAuthority,
        string oidcClientId,
        string? oidcClientSecret,
        DateTime utcNow)
    {
        IsEnabled = isEnabled;
        OidcAuthority = NormalizeAuthority(oidcAuthority);
        OidcClientId = oidcClientId.Trim();
        if (!string.IsNullOrWhiteSpace(oidcClientSecret))
            OidcClientSecret = oidcClientSecret.Trim();
        UpdatedAtUtc = utcNow;
    }

    public static string NormalizeAuthority(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
            throw new ArgumentException("OIDC authority is required.", nameof(authority));

        string value = authority.Trim().TrimEnd('/');
        if (!value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("OIDC authority must use HTTPS.", nameof(authority));

        return value;
    }
}
