using Microsoft.AspNetCore.Http;

namespace Shared.Errors
{
    /// <summary>
    /// Immutable base type for application errors returned via <c>Result&lt;TSuccess, Error&gt;</c>.
    /// Subclasses (e.g. <c>NotFoundError</c>, <c>ForbiddenError</c>) layer in semantic typing while
    /// keeping the shared HTTP-friendly shape (<see cref="ErrorType"/>, <see cref="StatusCode"/>,
    /// <see cref="ErrorMessage"/>).
    /// </summary>
    public class Error
    {
        public string ErrorType { get; init; }
        public int? StatusCode { get; init; }
        public string? ErrorMessage { get; init; }

        public Error(
            string errorType = "InternalServerError",
            int? statusCode = StatusCodes.Status500InternalServerError,
            string? errorMessage = null)
        {
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            StatusCode = statusCode;
        }
    }
}
