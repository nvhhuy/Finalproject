namespace Application.Contracts.Tenancy;

public enum TenantHostResolutionSource
{
    None = 0,
    Subdomain = 1,
    CustomDomain = 2,
}

public sealed record TenantHostResolution(
    Guid TenantId,
    string Slug,
    TenantHostResolutionSource Source);

public interface ITenantHostResolver
{
    Task<TenantHostResolution?> ResolveAsync(string host, CancellationToken cancellationToken = default);
}
