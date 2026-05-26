using Application.Contracts.Services;
using Domains.UserAggregate.ValueObjects;

namespace Infrastructure.Services;

/// <summary>
/// Default <see cref="ILoggedInUserService"/> for hosts without an HTTP context (Workers, scripts).
/// Behaves as an anonymous principal so background jobs that read this service for auditing still
/// function without throwing.
/// </summary>
public sealed class NullLoggedInUserService : ILoggedInUserService
{
    public UserId? UserId => null;
    public bool IsInRole(string role) => false;
}
