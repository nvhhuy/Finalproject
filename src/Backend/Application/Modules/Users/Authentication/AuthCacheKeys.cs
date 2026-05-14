namespace Application.Modules.Users.Authentication;

public static class AuthCacheKeys
{
    public const string PendingLoginPrefix = "auth:login-pending:";
    public const string PasswordResetPrefix = "auth:password-reset:";
    public const string EmailVerificationPrefix = "auth:email-verify:";
}
