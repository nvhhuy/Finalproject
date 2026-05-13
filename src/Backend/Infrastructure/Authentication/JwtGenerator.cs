using Application.Contracts.Services;
using Domains.UserAggregate;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Authentication;

public sealed class JwtGenerator : IJwtGenerator
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;

    public JwtGenerator(IDateTimeProvider dateTimeProvider, IOptions<JwtSettings> jwtOptions)
    {
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
    }

    public (string AccessToken, DateTime ExpiresUtc) CreateAccessToken(User user, Guid? impersonatorUserId = null)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role),
            new("two_factor", user.TwoFactorEnabled ? "1" : "0")
        };

        if (impersonatorUserId is not null)
            claims.Add(new Claim("impersonator_sub", impersonatorUserId.Value.ToString()));

        DateTime expires = _dateTimeProvider.Now.AddMinutes(_jwtSettings.ExpirationTimeInMinutes);
        var securityToken = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            signingCredentials: signingCredentials,
            notBefore: _dateTimeProvider.Now,
            expires: expires);

        string token = new JwtSecurityTokenHandler().WriteToken(securityToken);
        return (token, expires);
    }
}
