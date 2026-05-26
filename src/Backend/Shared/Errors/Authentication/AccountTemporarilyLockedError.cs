using Microsoft.AspNetCore.Http;

namespace Shared.Errors.Authentication;

/// <summary>Issued when too many recent failed login attempts triggered a temporary lockout.</summary>
public sealed class AccountTemporarilyLockedError : AuthenticationError
{
    public AccountTemporarilyLockedError(TimeSpan retryAfter)
        : base(
            "Authentication.TemporarilyLocked",
            StatusCodes.Status429TooManyRequests,
            $"Too many failed login attempts. Try again in {Math.Ceiling(retryAfter.TotalSeconds):0}s.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
