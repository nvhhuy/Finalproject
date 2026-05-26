using System.Net;

namespace Shared.Errors
{
    /// <summary>
    /// Immutable base type for application errors returned via <c>Result&lt;TSuccess, Error&gt;</c>.
    /// Subclasses (e.g. <c>NotFoundError</c>, <c>ForbiddenError</c>) layer in semantic typing while
    /// keeping the shared HTTP-friendly shape (<see cref="ErrorType"/>, <see cref="StatusCode"/>,
    /// <see cref="ErrorMessage"/>).
    /// </summary>
    /// <remarks>
    /// Uses <see cref="HttpStatusCode"/> values (BCL) rather than ASP.NET-specific
    /// <c>StatusCodes</c> constants so the <c>Shared</c> kernel does not transitively pull in
    /// <c>Microsoft.AspNetCore.*</c> and can be referenced by non-HTTP hosts (Worker, scripts).
    /// </remarks>
    public class Error
    {
        public string ErrorType { get; init; }
        public int? StatusCode { get; init; }
        public string? ErrorMessage { get; init; }

        public Error(
            string errorType = "InternalServerError",
            int? statusCode = (int)HttpStatusCode.InternalServerError,
            string? errorMessage = null)
        {
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            StatusCode = statusCode;
        }
    }
}
