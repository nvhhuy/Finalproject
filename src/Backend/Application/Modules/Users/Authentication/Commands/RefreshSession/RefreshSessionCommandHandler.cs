using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Security;

namespace Application.Modules.Users.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler : IRequestHandler<RefreshSessionCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserReadRepository _read;
    private readonly IUserWriteRepository _write;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IUnitOfWork _uow;

    public RefreshSessionCommandHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IAuthenticationTokenIssuer tokens,
        IUnitOfWork uow)
    {
        _read = read;
        _write = write;
        _tokens = tokens;
        _uow = uow;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        string hash = TokenHasher.Sha256(request.RefreshToken);
        RefreshToken? existing = await _write.GetRefreshTokenByHashForUpdateAsync(hash, cancellationToken);
        if (existing is null || !existing.IsActive)
            return new InvalidCredentialsError("Refresh token is invalid or expired.");

        existing.Revoke();
        await _uow.SaveChangesAsync(cancellationToken);

        User? user = await _read.GetByIdAsync(existing.UserId.Value, cancellationToken: cancellationToken);
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
