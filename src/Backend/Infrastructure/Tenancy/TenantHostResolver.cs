using Application.Contracts.Persistence;
using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Tenancy;

public sealed class TenantHostResolver : ITenantHostResolver
{
    private readonly IApplicationReadDbContext _db;
    private readonly TenancyHostOptions _options;

    public TenantHostResolver(IApplicationReadDbContext db, IOptions<TenancyHostOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<TenantHostResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        var normalizedHost = NormalizeHost(host);
        if (string.IsNullOrWhiteSpace(normalizedHost))
            return null;

        var baseHost = string.IsNullOrWhiteSpace(_options.SubdomainBaseHost)
            ? null
            : _options.SubdomainBaseHost.Trim().ToLowerInvariant();

        if (baseHost is not null)
        {
            if (normalizedHost == baseHost || normalizedHost == $"www.{baseHost}")
                return null;

            var suffix = $".{baseHost}";
            if (normalizedHost.EndsWith(suffix, StringComparison.Ordinal))
            {
                var subdomain = normalizedHost[..^suffix.Length];
                if (IsReservedSubdomain(subdomain))
                    return null;

                try
                {
                    var slug = Tenant.NormalizeSlug(subdomain);
                    return await ResolveBySlugAsync(slug, TenantHostResolutionSource.Subdomain, cancellationToken);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        return await ResolveCustomDomainAsync(normalizedHost, cancellationToken);
    }

    private async Task<TenantHostResolution?> ResolveBySlugAsync(
        string slug,
        TenantHostResolutionSource source,
        CancellationToken cancellationToken)
    {
        var row = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug && t.Status == TenantStatus.Active)
            .Select(t => new { t.Id, t.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new TenantHostResolution(row.Id.Value, row.Slug, source);
    }

    private async Task<TenantHostResolution?> ResolveCustomDomainAsync(
        string hostname,
        CancellationToken cancellationToken)
    {
        var row = await _db.TenantDomains
            .AsNoTracking()
            .Where(d => d.Hostname == hostname && d.Status == TenantDomainStatus.Active)
            .Join(
                _db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.Active),
                d => d.TenantId,
                t => t.Id,
                (d, t) => new { t.Id, t.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new TenantHostResolution(row.Id.Value, row.Slug, TenantHostResolutionSource.CustomDomain);
    }

    private bool IsReservedSubdomain(string subdomain)
    {
        return _options.ReservedSubdomains.Any(r =>
            string.Equals(r, subdomain, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return string.Empty;

        var value = host.Trim().ToLowerInvariant();
        var colon = value.IndexOf(':');
        if (colon > 0)
            value = value[..colon];
        return value;
    }
}