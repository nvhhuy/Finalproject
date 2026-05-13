using Application.Modules.Users.Authentication.Common;
using Shared.Errors;

namespace Application.Modules.Users.Authentication.Commands.Register
{
    public record RegisterCommand(
        string FirstName,
        string LastName,
        string UserName,
        string Email,
        string Password,
        string? RemoteIpAddress = null) : IRequest<Result<AuthenticationResponse, Error>>
    {
    }
}
