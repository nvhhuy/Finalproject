namespace Application.Contracts.Services;

/// <summary>
///     Validates Cloudflare Turnstile challenge tokens server-side using the siteverify API.
/// </summary>
public interface ITurnstileService
{
    /// <summary>
    ///     Returns <c>true</c> when the token is valid (or when Turnstile is disabled via
    ///     configuration). Returns <c>false</c> on invalid / missing tokens.
    /// </summary>
    Task<bool> ValidateAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default);
}
