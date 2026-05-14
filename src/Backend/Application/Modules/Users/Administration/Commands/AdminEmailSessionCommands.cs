using Application.Contracts.Administration;
using Application.Contracts.Caching;
using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication;
using Application.Modules.Users.Authentication.Commands.ForgotPassword;
using Application.Modules.Users.Authentication.Commands.VerifyEmail;
using Application.Modules.Users.Authentication.Options;
using Domains.UserAggregate;
using MediatR;
using Microsoft.Extensions.Options;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;
using Shared.Security;
using System.Security.Cryptography;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminMarkEmailVerifiedCommand(Guid ActingAdminUserId, Guid TargetUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>;

public sealed class AdminMarkEmailVerifiedCommandHandler : IRequestHandler<AdminMarkEmailVerifiedCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IAdminAuditLogWriter _audit;

    public AdminMarkEmailVerifiedCommandHandler(IUserRespository users, IAdminAuditLogWriter audit)
    {
        _users = users;
        _audit = audit;
    }

    public async Task<Result<bool, Error>> Handle(AdminMarkEmailVerifiedCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.MarkEmailVerified(DateTime.UtcNow);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserEmailVerified, request.TargetUserId, request.ActorIpAddress, null);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResendEmailVerificationCommand(Guid ActingAdminUserId, Guid TargetUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>;

public sealed class AdminResendEmailVerificationCommandHandler : IRequestHandler<AdminResendEmailVerificationCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly ICacheService _cache;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetEmailOptions _emailOptions;
    private readonly IAdminAuditLogWriter _audit;

    public AdminResendEmailVerificationCommandHandler(
        IUserRespository users,
        ICacheService cache,
        IEmailSender emailSender,
        IOptions<PasswordResetEmailOptions> emailOptions,
        IAdminAuditLogWriter audit)
    {
        _users = users;
        _cache = cache;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _audit = audit;
    }

    public async Task<Result<bool, Error>> Handle(AdminResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        if (target.EmailVerifiedAtUtc is not null)
            return new Error("AlreadyVerified", 400, "Email is already verified.");

        string tokenId = Guid.NewGuid().ToString("N");
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string tokenHash = TokenHasher.Sha256(token);
        string cacheKey = AuthCacheKeys.EmailVerificationPrefix + tokenId;

        int ttlMinutes = _emailOptions.TokenTtlMinutes > 0 ? _emailOptions.TokenTtlMinutes : 30;
        TimeSpan ttl = TimeSpan.FromMinutes(ttlMinutes);

        await _cache.SetAsync(cacheKey, new EmailVerificationTokenPayload(target.Id.Value, tokenHash), ttl, cancellationToken);

        string baseUrl = (_emailOptions.PublicAppBaseUrl ?? "").Trim().TrimEnd('/');
        string instructions;
        if (!string.IsNullOrEmpty(baseUrl))
        {
            string query = $"tokenId={Uri.EscapeDataString(tokenId)}&token={Uri.EscapeDataString(token)}";
            instructions =
                $"Confirm your email address (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"{baseUrl}/verify-email?{query}";
        }
        else
        {
            instructions =
                $"Use the app’s verify-email screen with these values (valid for {ttlMinutes} minutes):{Environment.NewLine}" +
                $"Token ID: {tokenId}{Environment.NewLine}" +
                $"Token: {token}";
        }

        string body =
            "Please confirm your email address." +
            $"{Environment.NewLine}{Environment.NewLine}{instructions}" +
            $"{Environment.NewLine}{Environment.NewLine}If you did not expect this message, contact support.";
        await _emailSender.SendAsync(target.Email, "Confirm your email", body, cancellationToken);

        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserEmailVerificationSent, request.TargetUserId, request.ActorIpAddress, null);
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminRevokeUserSessionsCommand(Guid ActingAdminUserId, Guid TargetUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>;

public sealed class AdminRevokeUserSessionsCommandHandler : IRequestHandler<AdminRevokeUserSessionsCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLogWriter _audit;

    public AdminRevokeUserSessionsCommandHandler(IUserRespository users, IDateTimeProvider clock, IAdminAuditLogWriter audit)
    {
        _users = users;
        _clock = clock;
        _audit = audit;
    }

    public async Task<Result<bool, Error>> Handle(AdminRevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        int revoked = await _users.RevokeAllRefreshTokensForUserAsync(request.TargetUserId, _clock.Now, cancellationToken);
        _audit.Append(
            request.ActingAdminUserId,
            AdminAuditActions.UserSessionsRevoked,
            request.TargetUserId,
            request.ActorIpAddress,
            $"tokens_revoked:{revoked}");
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}
