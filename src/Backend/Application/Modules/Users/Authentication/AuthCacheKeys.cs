namespace Application.Modules.Users.Authentication;

public static class AuthCacheKeys
{
    public const string PendingLoginPrefix = "auth:login-pending:";
    public const string PasswordResetPrefix = "auth:password-reset:";
    public const string EmailVerificationPrefix = "auth:email-verify:";

    /// <summary>Counter for 2FA brute-force protection, scoped to a single pending-login id.</summary>
    public const string TwoFactorAttemptsPrefix = "auth:2fa-attempts:";

    /// <summary>Marks a TOTP step as already consumed so the same 6-digit code can't be replayed within its 30-second window.</summary>
    public const string TwoFactorUsedStepPrefix = "auth:2fa-step:";
}
