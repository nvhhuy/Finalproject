using Application.Contracts.Services;
using Domains.UserAggregate.ValueObjects;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace Infrastructure.Services;

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
            string? sub = _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out Guid guid))
                return null;
            return UserId.Create(guid);
        }
    }
}
