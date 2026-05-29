using System.Net;

namespace Shared.Errors.Authentication;

public abstract class AuthenticationError : Error
{
    public AuthenticationError(string errorType, int statusCode = (int)HttpStatusCode.BadRequest,
        string? errorMessage = null) : base(errorType, statusCode, errorMessage)
    {
    }
}