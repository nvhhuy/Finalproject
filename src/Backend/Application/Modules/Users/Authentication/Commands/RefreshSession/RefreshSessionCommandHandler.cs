using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Services;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Security;

namespace Application.Modules.Users.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler : IRequestHandler<RefreshSessionCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserRespository _users;
    private readonly IAuthenticationTokenIssuer _tokens;

    public RefreshSessionCommandHandler(IUserRespository users, IAuthenticationTokenIssuer tokens)
    {
        _users = users;
        _tokens = tokens;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        string hash = TokenHasher.Sha256(request.RefreshToken);
        RefreshToken? existing = await _users.GetRefreshTokenByHashAsync(hash, cancellationToken);
        if (existing is null || !existing.IsActive)
            return new InvalidCredentialsError("Refresh token is invalid or expired.");

        existing.Revoke();
        await _users.SaveChangesAsync();

        User? user = await _users.GetUserByIdAsync(existing.UserId.Value, cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User no longer exists.");

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
