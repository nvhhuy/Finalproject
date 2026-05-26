using Application.Modules.Users.Authentication;
using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Domains.UserAggregate;
using MediatR;
using OtpNet;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;

public sealed class CompleteTwoFactorLoginCommandHandler : IRequestHandler<CompleteTwoFactorLoginCommand, Result<AuthenticationResponse, Error>>
{
    private readonly ICacheService _cache;
    private readonly IUserReadRepository _users;
    private readonly IAuthenticationTokenIssuer _tokens;

    public CompleteTwoFactorLoginCommandHandler(ICacheService cache, IUserReadRepository users, IAuthenticationTokenIssuer tokens)
    {
        _cache = cache;
        _users = users;
        _tokens = tokens;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(CompleteTwoFactorLoginCommand request, CancellationToken cancellationToken)
    {
        string key = AuthCacheKeys.PendingLoginPrefix + request.PendingLoginId;
        string? userIdStr = await _cache.GetAsync<string>(key, cancellationToken);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
            return new InvalidCredentialsError("Login session expired. Please sign in again.");

        User? user = await _users.GetByIdAsync(userId, cancellationToken: cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
            return new InvalidCredentialsError("Two-factor authentication failed.");

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out _, new VerificationWindow(previous: 1, future: 1)))
            return new InvalidCredentialsError("Invalid authenticator code.");

        await _cache.RemoveAsync(key, cancellationToken);

        Result<AuthenticationTokenPair, Error> pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }
}
