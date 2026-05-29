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

    // Security incidents that must be logged for forensic continuity even though no admin
    // initiated them. The actor user id matches the affected user.
    public const string UserRefreshTokenReuseDetected = "user.refresh_token_reuse_detected";

    // DLQ / messaging admin actions. The audit log is the source of truth for who poked at the
    // messaging plumbing, since RabbitMQ itself doesn't authenticate per-call (just per-connection).
    public const string MessagingDeadLetterPeeked = "messaging.dlq_peeked";
    public const string MessagingDeadLetterReplayed = "messaging.dlq_replayed";
    public const string MessagingDeadLetterDrained = "messaging.dlq_drained";

    public const string MemberNotificationBroadcast = "notifications.member_broadcast";
}
