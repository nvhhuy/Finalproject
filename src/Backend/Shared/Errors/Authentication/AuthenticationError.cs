using Microsoft.AspNetCore.Http;

namespace Shared.Errors.Authentication
{
    public abstract class AuthenticationError : Error
    {
        public AuthenticationError(string errorType, int statusCode = StatusCodes.Status400BadRequest, string? errorMessage = null) : base(errorType, statusCode, errorMessage)
        {

        }
    }
}
