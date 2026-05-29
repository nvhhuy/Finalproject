using Domains.UserAggregate;

namespace Application.Contracts.Services;

public sealed record AuthenticationTokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresUtc);

public interface IAuthenticationTokenIssuer
{
    Task<Result<AuthenticationTokenPair, Error>> IssueAsync(
        User user,
        string? remoteIpAddress,
        CancellationToken cancellationToken,
        Guid? impersonatorUserId = null);
}