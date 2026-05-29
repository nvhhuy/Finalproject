using Application.Modules.Users.Authentication.Common;

namespace Application.Modules.Users.Authentication.Queries;

public record LoginQuery(string Email, string Password, string? RemoteIpAddress = null)
    : IRequest<Result<AuthenticationResponse, Error>>
{
}