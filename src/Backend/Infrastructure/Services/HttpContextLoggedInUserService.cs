using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Contracts.Services;
using Domains.UserAggregate.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Services;

/// <summary>
///     Resolves the current principal id from the ambient <see cref="HttpContext" />.
/// </summary>
/// <remarks>
///     We accept either <see cref="ClaimTypes.NameIdentifier" /> or <see cref="JwtRegisteredClaimNames.Sub" />
///     to stay consistent with <c>ApiControllerBase.TryGetUserId</c>. ASP.NET Core's JWT handler maps
///     <c>sub</c> to <c>NameIdentifier</c> by default, but our handler disables that mapping, so the raw
///     <c>sub</c> claim is also still present in custom builds — checking both keeps callers robust.
/// </remarks>
public sealed class HttpContextLoggedInUserService : ILoggedInUserService
{
    public const string ImpersonatorClaimType = "impersonator_sub";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextLoggedInUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public UserId? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var guid))
                return null;
            return UserId.Create(guid);
        }
    }

    public UserId? ImpersonatorUserId
    {
        get
        {
            var impersonator = _httpContextAccessor.HttpContext?.User?
                .FindFirst(ImpersonatorClaimType)?.Value;
            if (string.IsNullOrEmpty(impersonator) || !Guid.TryParse(impersonator, out var guid))
                return null;
            return UserId.Create(guid);
        }
    }

    public bool IsImpersonating => ImpersonatorUserId is not null;

    public bool IsInRole(string role)
    {
        return _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
    }
}