using Microsoft.AspNetCore.Http;

namespace Shared.Errors.General
{
    public class NotFoundError : Error
    {
        public NotFoundError(
            string? errorMessage = null) : base(nameof(NotFoundError), StatusCodes.Status404NotFound, errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
