namespace Application.Contracts.Authentication;

/// <summary>
///     Tracks failed login attempts per identity (email or normalised username) and applies a
///     temporary lockout when the configured threshold is exceeded. Backed by the distributed
///     cache so it survives instance failover but resets automatically.
/// </summary>
public interface IFailedLoginTracker
{
    /// <summary>Returns the remaining lockout window if the identity is currently locked, otherwise <c>null</c>.</summary>
    Task<TimeSpan?> GetLockoutRemainingAsync(string identity, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a failed attempt. When the configured threshold is reached the identity becomes locked for
    ///     the configured lockout duration; subsequent failures extend the lockout.
    /// </summary>
    /// <returns><c>true</c> when this attempt caused (or extended) a lockout.</returns>
    Task<bool> RegisterFailureAsync(string identity, CancellationToken cancellationToken = default);

    /// <summary>Clears the failure counter and any active lockout after a successful sign-in.</summary>
    Task ResetAsync(string identity, CancellationToken cancellationToken = default);
}