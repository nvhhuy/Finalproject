namespace Application.Modules.Users.Authentication.Options;

/// <summary>Email and cache behavior for self-service password reset.</summary>
public sealed class PasswordResetEmailOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>
    ///     Public web app origin without a trailing slash (e.g. https://app.example.com). When set, emails include a
    ///     reset link.
    /// </summary>
    public string PublicAppBaseUrl { get; set; } = "";

    /// <summary>Lifetime for reset tokens in the distributed cache.</summary>
    public int TokenTtlMinutes { get; set; } = 30;
}