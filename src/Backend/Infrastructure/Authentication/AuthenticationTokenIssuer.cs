using Application.Contracts.Services;
using Domains.UserAggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;
using Shared.Security;

using System.Security.Cryptography;

namespace Infrastructure.Authentication;

public sealed class AuthenticationTokenIssuer : IAuthenticationTokenIssuer
{
    private readonly IJwtGenerator _jwtGenerator;
    private readonly ApplicationDbContext _db;
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly IDateTimeProvider _clock;

    public AuthenticationTokenIssuer(
        IJwtGenerator jwtGenerator,
        ApplicationDbContext db,
        IOptions<JwtSettings> jwtOptions,
        IDateTimeProvider clock)
    {
        _jwtGenerator = jwtGenerator;
        _db = db;
        _jwtOptions = jwtOptions;
        _clock = clock;
    }

    public async Task<Result<AuthenticationTokenPair, Error>> IssueAsync(
        User user,
        string? remoteIpAddress,
        CancellationToken cancellationToken,
        Guid? impersonatorUserId = null)
    {
        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

        (string accessToken, DateTime accessExpires) = _jwtGenerator.CreateAccessToken(user, impersonatorUserId);

        string refreshPlain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        string refreshHash = TokenHasher.Sha256(refreshPlain);
        DateTime refreshExpires = _clock.Now.AddDays(_jwtOptions.Value.RefreshTokenExpirationDays);

        User? tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id.Value == user.Id.Value, cancellationToken);
        if (tracked is null)
            return new NotFoundError("User was not found for token issuance.");

        tracked.RecordLogin();
        RefreshToken token = RefreshToken.Issue(tracked.Id, refreshHash, refreshExpires, _clock.Now, remoteIpAddress);
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthenticationTokenPair(accessToken, accessExpires, refreshPlain, refreshExpires);
    }
}
