using Application.Contracts.ExternalAuth;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalAuth;

public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly GoogleAuthSettings _settings;

    public GoogleIdTokenValidator(IOptions<GoogleAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId) || string.IsNullOrWhiteSpace(idToken))
            return null;

        try
        {
            var validation = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _settings.ClientId }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, validation);
            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                payload.GivenName,
                payload.FamilyName,
                payload.Name);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class GoogleAuthSettings
{
    public const string SectionName = "Authentication:Google";
    public string ClientId { get; init; } = string.Empty;
}