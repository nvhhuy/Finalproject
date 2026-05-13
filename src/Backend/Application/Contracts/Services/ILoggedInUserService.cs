using Domains.UserAggregate.ValueObjects;

namespace Application.Contracts.Services
{
    public interface ILoggedInUserService
    {
        UserId? UserId { get; }
    }
}
