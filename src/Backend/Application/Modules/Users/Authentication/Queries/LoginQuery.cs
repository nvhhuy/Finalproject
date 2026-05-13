using Application.Modules.Users.Authentication.Common;
using Shared.Errors;


namespace Application.Modules.Users.Authentication.Queries
{
    public record LoginQuery(string Email, string Password, string? RemoteIpAddress = null) : IRequest<Result<AuthenticationResponse, Error>>
    {
    }
}