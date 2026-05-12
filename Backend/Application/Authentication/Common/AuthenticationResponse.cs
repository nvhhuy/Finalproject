
using Domains.UserAggregate;


namespace Application.Authentication.Common
{
    public record AuthenticationResponse(User User, string Token)
    {

    }
}
