using System.Text.RegularExpressions;
using Domains.Common;
using Domains.Tenancy.ValueObjects;

namespace Domains.Tenancy;

public sealed partial class TenantDomain : Entity<Guid>
{
    private TenantDomain()
    {
    }

    private TenantDomain(
        Guid id,
        TenantId tenantId,
        string hostname,
        string verificationToken,
        DateTime createdAtUtc) : base(id)
    {
        TenantId = tenantId;
        Hostname = hostname;
        VerificationToken = verificationToken;
        Status = TenantDomainStatus.PendingVerification;
        CreatedAtUtc = createdAtUtc;
    }

    public TenantId TenantId { get; private set; } = null!;
    public string Hostname { get; private set; } = null!;
    public TenantDomainStatus Status { get; private set; }
    public string VerificationToken { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? VerifiedAtUtc { get; private set; }

    public static TenantDomain Create(TenantId tenantId, string hostname, string verificationToken, DateTime utcNow)
    {
        var normalized = NormalizeHostname(hostname);
        if (string.IsNullOrWhiteSpace(verificationToken))
            throw new ArgumentException("Verification token is required.", nameof(verificationToken));

        return new TenantDomain(Guid.NewGuid(), tenantId, normalized, verificationToken.Trim(), utcNow);
    }

    public void MarkVerified(DateTime utcNow)
    {
        Status = TenantDomainStatus.Verified;
        VerifiedAtUtc = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = TenantDomainStatus.Active;
        VerifiedAtUtc ??= utcNow;
    }

    public void Disable()
    {
        Status = TenantDomainStatus.Disabled;
    }

    public static string NormalizeHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("Hostname is required.", nameof(hostname));

        var value = hostname.Trim().ToLowerInvariant();
        var colon = value.IndexOf(':');
        if (colon > 0)
            value = value[..colon];

        if (value.StartsWith("https://", StringComparison.Ordinal))
            value = value["https://".Length..];
        if (value.StartsWith("http://", StringComparison.Ordinal))
            value = value["http://".Length..];
        if (value.EndsWith('/'))
            value = value.TrimEnd('/');

        if (value.Length is < 4 or > 253)
            throw new ArgumentException("Hostname length is invalid.", nameof(hostname));
        if (!HostnamePattern().IsMatch(value))
            throw new ArgumentException("Hostname format is invalid.", nameof(hostname));

        return value;
    }

    public static string CreateVerificationToken()
    {
        return Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex(@"^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,}$")]
    private static partial Regex HostnamePattern();
}