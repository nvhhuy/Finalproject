using Application.Authentication.Common;
using Shared.Errors;


namespace Application.Authentication.Queries
{
    public record LoginQuery(string Email, string Password) : IRequest<Result<AuthenticationResponse, Error>>
    {

    }
}