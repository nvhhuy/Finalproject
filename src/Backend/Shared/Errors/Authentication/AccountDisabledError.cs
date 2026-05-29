using System.Net;

namespace Shared.Errors.Authentication;

public sealed class AccountDisabledError : AuthenticationError
{
    public AccountDisabledError(string message) : base("Account.Disabled", (int)HttpStatusCode.Forbidden, message)
    {
    }
}