using Application.Modules.Users.Authentication.Common;
using Application.Contracts.ExternalAuth;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using Microsoft.EntityFrameworkCore;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Commands.GoogleSignIn;

public sealed class GoogleSignInCommandHandler : IRequestHandler<GoogleSignInCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IGoogleIdTokenValidator _google;
    private readonly IApplicationReadDbContext _read;
    private readonly IUserWriteRepository _users;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public GoogleSignInCommandHandler(
        IGoogleIdTokenValidator google,
        IApplicationReadDbContext read,
        IUserWriteRepository users,
        IAuthenticationTokenIssuer tokens,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _google = google;
        _read = read;
        _users = users;
        _tokens = tokens;
        _clock = clock;
        _uow = uow;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        GoogleUserInfo? info = await _google.ValidateAsync(request.IdToken, cancellationToken);
        if (info is null)
            return new InvalidCredentialsError("Google token could not be validated.");

        ExternalUserLogin? link = await _read.ExternalLogins
            .FirstOrDefaultAsync(
                e => e.Provider == ExternalLoginProviders.Google && e.ProviderSubject == info.Subject,
                cancellationToken);

        User? user;
        if (link is not null)
        {
            user = await _read.Users.FirstOrDefaultAsync(u => u.Id.Value == link.UserId.Value, cancellationToken);
            if (user is null)
                return new InvalidCredentialsError("Linked user was not found.");
        }
        else
        {
            user = await _read.Users.FirstOrDefaultAsync(u => u.Email == info.Email, cancellationToken);
            if (user is null)
            {
                string userName = await BuildUniqueUserNameAsync(info.Email, cancellationToken);
                string first = info.GivenName ?? info.Name ?? "User";
                string last = info.FamilyName ?? string.Empty;
                user = User.CreateForExternal(first, last, userName, info.Email);
                user.MarkRegisteredAt(_clock.Now);
                user.MarkEmailVerified(_clock.Now);
                _users.AddUser(user);
                _users.AddExternalLogin(ExternalUserLogin.Create(user.Id, ExternalLoginProviders.Google, info.Subject));
                await _uow.SaveChangesAsync(cancellationToken);
                await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
            }
            else
            {
                bool alreadyLinked = await _read.ExternalLogins.AnyAsync(
                    e => e.UserId.Value == user.Id.Value
                         && e.Provider == ExternalLoginProviders.Google
                         && e.ProviderSubject == info.Subject,
                    cancellationToken);
                if (!alreadyLinked)
                {
                    _users.AddExternalLogin(ExternalUserLogin.Create(user.Id, ExternalLoginProviders.Google, info.Subject));
                    await _uow.SaveChangesAsync(cancellationToken);
                    await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
                }
            }
        }

        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

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

    private async Task<string> BuildUniqueUserNameAsync(string email, CancellationToken cancellationToken)
    {
        string local = email.Split('@')[0];
        string candidate = local;
        int i = 0;
        while (await _read.Users.AnyAsync(u => u.UserName == candidate, cancellationToken))
        {
            i++;
            candidate = $"{local}{i}";
        }
        return candidate;
    }
}
