using Microsoft.AspNetCore.Http;

namespace Shared.Errors.Authentication;

public sealed class TwoFactorRequiredError : AuthenticationError
{
    public string PendingLoginId { get; }
    public TwoFactorRequiredError(string pendingLoginId)
        : base("Authentication.TwoFactorRequired", StatusCodes.Status202Accepted, "Two-factor authentication is required.")
    {
        PendingLoginId = pendingLoginId;
    }
}
