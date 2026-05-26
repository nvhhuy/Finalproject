using System.Net;

namespace Shared.Errors.Authentication;

public sealed class TwoFactorRequiredError : AuthenticationError
{
    public string PendingLoginId { get; }
    public TwoFactorRequiredError(string pendingLoginId)
        : base("Authentication.TwoFactorRequired", (int)HttpStatusCode.Accepted, "Two-factor authentication is required.")
    {
        PendingLoginId = pendingLoginId;
    }
}
