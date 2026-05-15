using Application.Contracts.Administration;
using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Services;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

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
    private readonly IEmailVerificationSender _emailVerification;
    private readonly IAdminAuditLogWriter _audit;

    public AdminResendEmailVerificationCommandHandler(
        IUserRespository users,
        IEmailVerificationSender emailVerification,
        IAdminAuditLogWriter audit)
    {
        _users = users;
        _emailVerification = emailVerification;
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

        await _emailVerification.SendVerificationEmailAsync(target.Id.Value, target.Email, cancellationToken);
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
