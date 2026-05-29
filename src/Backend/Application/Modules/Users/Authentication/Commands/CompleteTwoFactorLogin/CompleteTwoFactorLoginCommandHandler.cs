using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication.Common;
using OtpNet;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Authentication.Commands.CompleteTwoFactorLogin;

public sealed class
    CompleteTwoFactorLoginCommandHandler : IRequestHandler<CompleteTwoFactorLoginCommand,
    Result<AuthenticationResponse, Error>>
{
    /// <summary>How many wrong codes we accept on the same pending-login id before invalidating it.</summary>
    private const int MaxAttemptsPerPendingLogin = 5;

    /// <summary>Window the TOTP verifier accepts (current ±1 = 90 seconds).</summary>
    private const int TotpVerificationWindowSteps = 1;

    /// <summary>How long a successfully used TOTP step is remembered so the same code can't be replayed.</summary>
    private static readonly TimeSpan UsedStepTtl = TimeSpan.FromMinutes(2);

    private readonly ICacheService _cache;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IUserReadRepository _users;

    public CompleteTwoFactorLoginCommandHandler(ICacheService cache, IUserReadRepository users,
        IAuthenticationTokenIssuer tokens)
    {
        _cache = cache;
        _users = users;
        _tokens = tokens;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(CompleteTwoFactorLoginCommand request,
        CancellationToken cancellationToken)
    {
        var pendingKey = AuthCacheKeys.PendingLoginPrefix + request.PendingLoginId;
        var userIdStr = await _cache.GetAsync<string>(pendingKey, cancellationToken);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return new InvalidCredentialsError("Login session expired. Please sign in again.");

        // Per-pending-id attempt counter. A determined attacker that already stole the
        // PendingLoginId cookie has a 5-minute window to brute-force the 6-digit code; we cap
        // that at 5 wrong codes total. Counter shares the same TTL as the pending login so it
        // disappears with the session.
        var attemptsKey = AuthCacheKeys.TwoFactorAttemptsPrefix + request.PendingLoginId;
        var attempts = await _cache.GetAsync<int?>(attemptsKey, cancellationToken) ?? 0;
        if (attempts >= MaxAttemptsPerPendingLogin)
        {
            // Burn the pending-login so the attacker has to start from password again.
            await _cache.RemoveAsync(pendingKey, cancellationToken);
            await _cache.RemoveAsync(attemptsKey, cancellationToken);
            return new InvalidCredentialsError("Too many attempts. Please sign in again.");
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken: cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            await IncrementAttemptsAsync(attemptsKey, attempts, cancellationToken);
            return new InvalidCredentialsError("Two-factor authentication failed.");
        }

        var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecret));
        if (!totp.VerifyTotp(request.Code, out var usedStep,
                new VerificationWindow(TotpVerificationWindowSteps, TotpVerificationWindowSteps)))
        {
            await IncrementAttemptsAsync(attemptsKey, attempts, cancellationToken);
            return new InvalidCredentialsError("Invalid authenticator code.");
        }

        // Replay protection: TOTP allows a 30-second step + a ±1 verification window so the same
        // 6-digit code is technically valid for ~90 seconds. We remember the step that just
        // succeeded for 2 minutes (longer than the window) and reject re-use during that period.
        var usedStepKey = AuthCacheKeys.TwoFactorUsedStepPrefix + user.Id.Value + ":" + usedStep;
        var alreadyUsed = await _cache.GetAsync<bool?>(usedStepKey, cancellationToken) == true;
        if (alreadyUsed)
        {
            await IncrementAttemptsAsync(attemptsKey, attempts, cancellationToken);
            return new InvalidCredentialsError("Invalid authenticator code.");
        }

        await _cache.SetAsync(usedStepKey, true, UsedStepTtl, cancellationToken);

        await _cache.RemoveAsync(pendingKey, cancellationToken);
        await _cache.RemoveAsync(attemptsKey, cancellationToken);

        var pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }

    private Task IncrementAttemptsAsync(string key, int current, CancellationToken cancellationToken)
    {
        // Match the pending-login TTL (5 minutes) so the counter expires alongside the session
        // instead of leaking memory in the cache layer.
        return _cache.SetAsync(key, current + 1, TimeSpan.FromMinutes(5), cancellationToken);
    }
}