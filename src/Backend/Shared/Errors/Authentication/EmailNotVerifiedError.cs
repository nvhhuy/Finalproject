namespace Shared.Errors.Authentication;

public sealed class EmailNotVerifiedError : Error
{
    public EmailNotVerifiedError()
        : base("EmailNotVerified", 403,
            "Please verify your email address before signing in. Check your inbox or request a new verification link.")
    {
    }
}