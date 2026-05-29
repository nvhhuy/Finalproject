using System.IdentityModel.Tokens.Jwt;
using Application.Contracts.ExternalAuth;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.ExternalAuth;

public sealed class TenantOidcIdTokenValidator : ITenantOidcIdTokenValidator
{
    private readonly ILogger<TenantOidcIdTokenValidator> _logger;

    public TenantOidcIdTokenValidator(ILogger<TenantOidcIdTokenValidator> logger)
    {
        _logger = logger;
    }

    public async Task<TenantOidcUserInfo?> ValidateAsync(
        string authority,
        string clientId,
        string idToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var config = await configManager.GetConfigurationAsync(cancellationToken);
            var handler = new JwtSecurityTokenHandler();
            TokenValidationParameters parameters = new()
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                IssuerSigningKeys = config.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            handler.ValidateToken(idToken, parameters, out var validated);
            if (validated is not JwtSecurityToken jwt)
                return null;

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
                return null;

            return new TenantOidcUserInfo(
                sub,
                email,
                jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value,
                jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value,
                jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value);
        }
        catch (Exception ex) when (ex is SecurityTokenException or IOException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Tenant OIDC token validation failed for authority {Authority}", authority);
            return null;
        }
    }
}