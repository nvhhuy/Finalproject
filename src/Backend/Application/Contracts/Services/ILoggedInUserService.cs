using Domains.UserAggregate.ValueObjects;

namespace Application.Contracts.Services
{
    public interface ILoggedInUserService
    {
        UserId? UserId { get; }

        /// <summary>Returns true when the current principal carries the given role claim.</summary>
        bool IsInRole(string role);
    }
}
