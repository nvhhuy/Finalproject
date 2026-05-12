using Domains.UserAggregate;


namespace Application.Contracts.Services
{
    public interface IJwtGenerator
    {
        string CreateToken(User user);
    }
}
