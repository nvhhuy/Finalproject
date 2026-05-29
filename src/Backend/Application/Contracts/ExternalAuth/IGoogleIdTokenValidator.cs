namespace Application.Contracts.ExternalAuth;

/// <summary>Validated Google ID token (from mobile / SPA Google Sign-In).</summary>
public sealed record GoogleUserInfo(string Subject, string Email, string? GivenName, string? FamilyName, string? Name);

public interface IGoogleIdTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}