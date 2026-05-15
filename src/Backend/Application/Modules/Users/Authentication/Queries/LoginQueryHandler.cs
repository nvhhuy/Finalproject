using Application.Modules.Users.Authentication;
using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Extensions;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Queries;

public sealed class LoginQueryHandler : IRequestHandler<LoginQuery, Result<AuthenticationResponse, Error>>
{
    private readonly IUserReadRepository _usersRead;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICacheService _cache;

    public LoginQueryHandler(
        IUserReadRepository usersRead,
        IAuthenticationTokenIssuer tokens,
        IPasswordHasher<User> passwordHasher,
        ICacheService cache)
    {
        _usersRead = usersRead;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _cache = cache;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validationResult = await request.ValidateAsync(new LoginQueryValidator(), cancellationToken);
        if (validationResult.IsFailure)
            return validationResult.Error;

        User? user = await _usersRead.GetByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("Invalid email or password.");

        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

        if (user.EmailVerifiedAtUtc is null)
            return new EmailNotVerifiedError();

        PasswordVerificationResult passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);
        if (passwordVerificationResult == PasswordVerificationResult.Failed)
            return new InvalidCredentialsError("Invalid email or password.");

        if (user.TwoFactorEnabled)
        {
            string pendingId = Guid.NewGuid().ToString("N");
            await _cache.SetAsync(
                AuthCacheKeys.PendingLoginPrefix + pendingId,
                user.Id.Value.ToString(),
                TimeSpan.FromMinutes(5),
                cancellationToken);
            return new TwoFactorRequiredError(pendingId);
        }

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
