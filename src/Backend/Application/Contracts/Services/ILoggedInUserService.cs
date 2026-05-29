using Domains.UserAggregate.ValueObjects;

namespace Application.Contracts.Services;

public interface ILoggedInUserService
{
    UserId? UserId { get; }

    /// <summary>Admin user id when the session was created via impersonation; otherwise null.</summary>
    UserId? ImpersonatorUserId { get; }

    /// <summary>True when <see cref="ImpersonatorUserId" /> is present.</summary>
    bool IsImpersonating { get; }

    /// <summary>Returns true when the current principal carries the given role claim.</summary>
    bool IsInRole(string role);
}