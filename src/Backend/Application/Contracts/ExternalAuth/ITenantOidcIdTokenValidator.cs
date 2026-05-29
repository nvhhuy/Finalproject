namespace Application.Contracts.ExternalAuth;

public sealed record TenantOidcUserInfo(
    string Subject,
    string Email,
    string? GivenName,
    string? FamilyName,
    string? Name);

public interface ITenantOidcIdTokenValidator
{
    Task<TenantOidcUserInfo?> ValidateAsync(
        string authority,
        string clientId,
        string idToken,
        CancellationToken cancellationToken = default);
}