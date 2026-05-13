using Microsoft.AspNetCore.Http;

namespace Shared.Errors.Authentication;

public sealed class AccountDisabledError : AuthenticationError
{
    public AccountDisabledError(string message) : base("Account.Disabled", StatusCodes.Status403Forbidden, message) { }
}
