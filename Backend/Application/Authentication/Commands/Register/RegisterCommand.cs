using Application.Authentication.Common;
using Shared.Errors;

namespace Application.Authentication.Commands.Register
{
    public record RegisterCommand(string FirstName, string LastName, string UserName, string Email, string Password) : IRequest<Result<AuthenticationResponse, Error>>
    {
    }
}
