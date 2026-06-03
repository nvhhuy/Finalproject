using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

public sealed class EmailVerifiedAuthorizationHandler : AuthorizationHandler<EmailVerifiedRequirement>
{
    private readonly ILoggedInUserService _loggedIn;
    private readonly IUserReadRepository _users;

    public EmailVerifiedAuthorizationHandler(ILoggedInUserService loggedIn, IUserReadRepository users)
    {
        _loggedIn = loggedIn;
        _users = users;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmailVerifiedRequirement requirement)
    {
        if (_loggedIn.UserId is null)
            return;

        if (_loggedIn.IsImpersonating)
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.IsInRole(UserRoles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        var user = await _users.GetByIdAsync(_loggedIn.UserId.Value);
        if (user?.EmailVerifiedAtUtc is not null)
            context.Succeed(requirement);
    }
}
