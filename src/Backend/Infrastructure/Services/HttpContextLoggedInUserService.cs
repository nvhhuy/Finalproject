using Application.Contracts.Services;
using Domains.UserAggregate.ValueObjects;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Infrastructure.Services;

/// <summary>
/// Resolves the current principal id from the ambient <see cref="HttpContext"/>.
/// </summary>
/// <remarks>
/// We accept either <see cref="ClaimTypes.NameIdentifier"/> or <see cref="JwtRegisteredClaimNames.Sub"/>
/// to stay consistent with <c>ApiControllerBase.TryGetUserId</c>. ASP.NET Core's JWT handler maps
/// <c>sub</c> to <c>NameIdentifier</c> by default, but our handler disables that mapping, so the raw
/// <c>sub</c> claim is also still present in custom builds — checking both keeps callers robust.
/// </remarks>
public sealed class HttpContextLoggedInUserService : ILoggedInUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextLoggedInUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public UserId? UserId
    {
        get
        {
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            string? sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out Guid guid))
                return null;
            return UserId.Create(guid);
        }
    }

    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User?.IsInRole(role) ?? false;
}
