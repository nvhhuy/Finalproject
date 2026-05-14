namespace Application.Modules.Users.Administration;

public static class AdminAuditActions
{
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";
    public const string UserLocked = "user.locked";
    public const string UserUnlocked = "user.unlocked";
    public const string UserImpersonated = "user.impersonated";
    public const string UserTwoFactorReset = "user.twofactor_reset";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserEmailVerified = "user.email_verified";
    public const string UserEmailVerificationSent = "user.email_verification_sent";
    public const string UserSessionsRevoked = "user.sessions_revoked";
}
