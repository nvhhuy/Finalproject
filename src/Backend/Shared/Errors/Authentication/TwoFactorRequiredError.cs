using System.Net;

namespace Shared.Errors.Authentication;

public sealed class TwoFactorRequiredError : AuthenticationError
{
    public TwoFactorRequiredError(string pendingLoginId)
        : base("Authentication.TwoFactorRequired", (int)HttpStatusCode.Accepted,
            "Two-factor authentication is required.")
    {
        PendingLoginId = pendingLoginId;
    }

    public string PendingLoginId { get; }
}