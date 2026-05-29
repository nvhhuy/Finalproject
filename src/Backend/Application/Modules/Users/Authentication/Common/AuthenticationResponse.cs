using Domains.UserAggregate;

namespace Application.Modules.Users.Authentication.Common;

public sealed record AuthenticationResponse(
    User User,
    string AccessToken,
    DateTime AccessTokenExpiresUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresUtc);