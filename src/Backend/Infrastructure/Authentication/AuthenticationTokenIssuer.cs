using System.Security.Cryptography;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;
using Shared.Security;

namespace Infrastructure.Authentication;

public sealed class AuthenticationTokenIssuer : IAuthenticationTokenIssuer
{
    private readonly IDateTimeProvider _clock;
    private readonly ApplicationDbContext _db;
    private readonly IJwtGenerator _jwtGenerator;
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly IUnitOfWork _uow;

    public AuthenticationTokenIssuer(
        IJwtGenerator jwtGenerator,
        ApplicationDbContext db,
        IOptions<JwtSettings> jwtOptions,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _jwtGenerator = jwtGenerator;
        _db = db;
        _jwtOptions = jwtOptions;
        _clock = clock;
        _uow = uow;
    }

    public async Task<Result<AuthenticationTokenPair, Error>> IssueAsync(
        User user,
        string? remoteIpAddress,
        CancellationToken cancellationToken,
        Guid? impersonatorUserId = null)
    {
        if (!user.CanSignIn)
            return new AccountDisabledError("This account cannot sign in.");

        var (accessToken, accessExpires) = _jwtGenerator.CreateAccessToken(user, impersonatorUserId);

        var refreshPlain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshHash = TokenHasher.Sha256(refreshPlain);
        var refreshExpires = _clock.Now.AddDays(_jwtOptions.Value.RefreshTokenExpirationDays);

        var tracked = await _db.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);
        if (tracked is null)
            return new NotFoundError("User was not found for token issuance.");

        tracked.RecordLogin();
        var token = RefreshToken.Issue(tracked.Id, refreshHash, refreshExpires, _clock.Now, remoteIpAddress);
        _db.RefreshTokens.Add(token);
        await _uow.SaveChangesAsync(cancellationToken);

        return new AuthenticationTokenPair(accessToken, accessExpires, refreshPlain, refreshExpires);
    }
}