using System.Net;

namespace Shared.Errors.General
{
    public class NotFoundError : Error
    {
        public NotFoundError(
            string? errorMessage = null) : base(nameof(NotFoundError), (int)HttpStatusCode.NotFound, errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
