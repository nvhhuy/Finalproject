using Microsoft.AspNetCore.Http;

namespace Shared.Errors.General;

public sealed class ForbiddenError : Error
{
    public ForbiddenError(string? errorMessage = null) : base(nameof(ForbiddenError), StatusCodes.Status403Forbidden, errorMessage) { }
}
