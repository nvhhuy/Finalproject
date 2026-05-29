using System.Net;

namespace Shared.Errors.General;

public class BadRequestError : Error
{
    protected BadRequestError(string errorType = nameof(BadRequestError),
        int statusCode = (int)HttpStatusCode.BadRequest, string? errorMessage = null) : base(errorType, statusCode,
        errorMessage)
    {
    }
}