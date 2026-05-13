using Domains.UserAggregate;

namespace Api.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
}
