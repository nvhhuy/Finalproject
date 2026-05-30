namespace Infrastructure.Services;

/// <summary>
///     Cloudflare Turnstile server-side configuration. Bound from the <c>Turnstile</c> section.
/// </summary>
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    /// <summary>
    ///     When <c>false</c> (default in Development), the <see cref="ITurnstileService" /> always
    ///     returns <c>true</c> without calling Cloudflare. Set to <c>true</c> in production and
    ///     provide a valid <see cref="SecretKey" />.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     The Cloudflare Turnstile <em>Secret Key</em> obtained from the Cloudflare dashboard.
    ///     Keep out of source control — use an environment variable or secrets manager.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    ///     Override the Cloudflare siteverify endpoint (useful for tests / private deployments).
    ///     Defaults to <c>https://challenges.cloudflare.com/turnstile/v0/siteverify</c>.
    /// </summary>
    public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}
