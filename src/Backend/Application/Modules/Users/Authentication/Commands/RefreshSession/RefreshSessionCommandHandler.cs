using Application.Contracts.Administration;
using Application.Modules.Users.Administration;
using Application.Modules.Users.Authentication.Common;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Security;

namespace Application.Modules.Users.Authentication.Commands.RefreshSession;

public sealed class RefreshSessionCommandHandler : IRequestHandler<RefreshSessionCommand, Result<AuthenticationResponse, Error>>
{
    private readonly IUserReadRepository _read;
    private readonly IUserWriteRepository _write;
    private readonly IAuthenticationTokenIssuer _tokens;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLogWriter _audit;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<RefreshSessionCommandHandler> _logger;

    public RefreshSessionCommandHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IAuthenticationTokenIssuer tokens,
        IUnitOfWork uow,
        IDateTimeProvider clock,
        IAdminAuditLogWriter audit,
        ICorrelationContext correlation,
        ILogger<RefreshSessionCommandHandler> logger)
    {
        _read = read;
        _write = write;
        _tokens = tokens;
        _uow = uow;
        _clock = clock;
        _audit = audit;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task<Result<AuthenticationResponse, Error>> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        string hash = TokenHasher.Sha256(request.RefreshToken);
        RefreshToken? existing = await _write.GetRefreshTokenByHashForUpdateAsync(hash, cancellationToken);
        if (existing is null)
            return new InvalidCredentialsError("Refresh token is invalid or expired.");

        // Theft detection: a token that has already been revoked is being replayed. This is the
        // signature of an exfiltrated cookie/local-storage value — the legitimate client rotates
        // and would never present a revoked token. Defensive response: revoke every active token
        // for this user so the attacker (and the victim) both have to re-authenticate, and emit
        // a high-severity log line so monitoring can alert.
        if (existing.RevokedAtUtc is not null)
        {
            int revokedCount = await _write.RevokeAllRefreshTokensForUserAsync(
                existing.UserId.Value,
                _clock.Now,
                cancellationToken);

            // Forensic continuity: write the incident to the admin audit log alongside the
            // warning log entry. Actor = affected user (no admin was involved); details capture
            // the LB-relayed IP and our correlation id so an analyst can pivot to request logs.
            _audit.Append(
                actorUserId: existing.UserId.Value,
                action: AdminAuditActions.UserRefreshTokenReuseDetected,
                targetUserId: existing.UserId.Value,
                ipAddress: request.RemoteIpAddress,
                details: $"correlation={_correlation.Current ?? "(none)"};revokedSessions={revokedCount}");

            await _uow.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Refresh-token reuse detected for user {UserId} from {Ip} (correlation={CorrelationId}); revoked {Count} active sessions.",
                existing.UserId.Value,
                request.RemoteIpAddress ?? "(unknown)",
                _correlation.Current ?? "(none)",
                revokedCount);

            return new InvalidCredentialsError("Refresh token is invalid or expired.");
        }

        if (!existing.IsActive)
            return new InvalidCredentialsError("Refresh token is invalid or expired.");

        existing.Revoke();
        await _uow.SaveChangesAsync(cancellationToken);

        User? user = await _read.GetByIdAsync(existing.UserId.Value, cancellationToken: cancellationToken);
        if (user is null)
            return new InvalidCredentialsError("User no longer exists.");

        Result<AuthenticationTokenPair, Error> pair = await _tokens.IssueAsync(user, request.RemoteIpAddress, cancellationToken);
        if (pair.IsFailure)
            return pair.Error;

        return new AuthenticationResponse(
            user,
            pair.Value.AccessToken,
            pair.Value.AccessTokenExpiresUtc,
            pair.Value.RefreshToken,
            pair.Value.RefreshTokenExpiresUtc);
    }
}
