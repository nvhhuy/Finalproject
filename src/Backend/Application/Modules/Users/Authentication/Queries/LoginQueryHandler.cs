using Application.Contracts.Authentication;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Notifications;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication.Common;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Queries;

public sealed class LoginQueryHandler : IRequestHandler<LoginQuery, Result<AuthenticationResponse, Error>>
{
    // Cached PBKDF2 hash used for the "user does not exist" branch so VerifyHashedPassword always
    // runs once per login attempt regardless of whether the email is registered. Without this the
    // PBKDF2 work item creates a ~tens-of-milliseconds timing gap between known and unknown emails
    // that lets an attacker enumerate the user table by clock alone. Volatile + null-check =
    // benign double-compute on race, but never null after first set.
    private static volatile string? _cachedDummyHash;
    private readonly ICacheService _cache;
    private readonly IFailedLoginTracker _failedLogins;
    private readonly IUserNotificationService _notifications;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IUserReadRepository _usersRead;

    public LoginQueryHandler(
        IUserReadRepository usersRead,
        IAuthenticationTokenIssuer tokens,
        IPasswordHasher<User> passwordHasher,
        ICacheService cache,
        IFailedLoginTracker failedLogins,
        IUserNotificationService notifications)
    {
        _usersRead = usersRead;
        _tokens = tokens;
        _passwordHasher = passwordHasher;
        _cache = cache;
        _failedLogins = failedLogins;
        _notifications = notifications;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(LoginQuery request,
        CancellationToken cancellationToken)
    {
        var lockoutRemaining = await _failedLogins.GetLockoutRemainingAsync(request.Email, cancellationToken);
        if (lockoutRemaining is not null)
            return new AccountTemporarilyLockedError(lockoutRemaining.Value);

        var user = await _usersRead.GetByEmailAsync(request.Email, cancellationToken: cancellationToken);

        // Anti-enumeration: when the email isn't registered we still spend a full PBKDF2 verify
        // against a dummy hash so the response time looks identical to a known-email + wrong-
        // password attempt. Returning the same InvalidCredentialsError on both branches removes
        // the response-shape oracle as well.
        if (user is null)
        {
            _ = _passwordHasher.VerifyHashedPassword(null!, GetOrComputeDummyHash(), request.Password);
            await _failedLogins.RegisterFailureAsync(request.Email, cancellationToken);
            return new InvalidCredentialsError("Invalid email or password.");
        }

        var passwordVerificationResult =
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (passwordVerificationResult == PasswordVerificationResult.Failed)
        {
            var nowLocked = await _failedLogins.RegisterFailureAsync(request.Email, cancellationToken);
            if (nowLocked)
            {
                var remaining = await _failedLogins.GetLockoutRemainingAsync(request.Email, cancellationToken);
                if (remaining is not null)
                    return new AccountTemporarilyLockedError(remaining.Value);
            }

            return new InvalidCredentialsError("Invalid email or password.");
        }

        // Password was correct: only NOW do we surface account-state errors. Returning these
        // before the password check would leak which email addresses are registered.
        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

        if (user.EmailVerifiedAtUtc is null)
            return new EmailNotVerifiedError();

        await _failedLogins.ResetAsync(request.Email, cancellationToken);

        if (user.TwoFactorEnabled)
        {
            var pendingId = Guid.NewGuid().ToString("N");
            await _cache.SetAsync(
                AuthCacheKeys.PendingLoginPrefix + pendingId,
                user.Id.Value.ToString(),
                TimeSpan.FromMinutes(5),
                cancellationToken);
            return new TwoFactorRequiredError(pendingId);
        }

        var pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        try
        {
            await _notifications.CreateAndDispatchAsync(
                user.Id.Value,
                "Signed in",
                "You have a new session on Base Project.",
                "/app",
                cancellationToken);
        }
        catch
        {
            // Login must succeed even if notification persistence or push fails.
        }

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }

    private string GetOrComputeDummyHash()
    {
        // Lazy double-checked init. The hash is computed via the DI-injected IPasswordHasher so it
        // carries whatever iteration count the deployment configured (default 600 000 after the
        // hardening pass). Using a cached dummy hash means the unknown-user branch executes the
        // exact same PBKDF2 work as the known-user branch.
        var cached = _cachedDummyHash;
        if (cached is not null)
            return cached;
        var dummy = User.CreateForLocalLoginPendingHash("D", "D", "ctdummy", "dummy@local.invalid");
        var computed = _passwordHasher.HashPassword(dummy, "__constant_time_dummy__");
        _cachedDummyHash = computed;
        return computed;
    }
}