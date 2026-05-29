using System.Net;

namespace Shared.Errors.General;

public sealed class ForbiddenError : Error
{
    public ForbiddenError(string? errorMessage = null) : base(nameof(ForbiddenError), (int)HttpStatusCode.Forbidden,
        errorMessage)
    {
    }
}