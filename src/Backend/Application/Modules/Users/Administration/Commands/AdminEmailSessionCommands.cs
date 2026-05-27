using Application.Common;
using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminMarkEmailVerifiedCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminMarkEmailVerifiedCommandHandler : IRequestHandler<AdminMarkEmailVerifiedCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _write;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public AdminMarkEmailVerifiedCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminMarkEmailVerifiedCommand request, CancellationToken cancellationToken)
    {
        User? target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        Error? conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.MarkEmailVerified(DateTime.UtcNow);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserEmailVerified, request.TargetUserId, request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResendEmailVerificationCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminResendEmailVerificationCommandHandler : IRequestHandler<AdminResendEmailVerificationCommand, Result<bool, Error>>
{
    private readonly IUserReadRepository _read;
    private readonly IEmailVerificationSender _emailVerification;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public AdminResendEmailVerificationCommandHandler(
        IUserReadRepository read,
        IEmailVerificationSender emailVerification,
        IAdminAuditLogWriter audit,
        IUnitOfWork uow)
    {
        _read = read;
        _emailVerification = emailVerification;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminResendEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        User? target = await _read.GetByIdAsync(request.TargetUserId, cancellationToken: cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        Error? conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        if (target.EmailVerifiedAtUtc is not null)
            return new Error("AlreadyVerified", 400, "Email is already verified.");

        await _emailVerification.SendVerificationEmailAsync(target.Id.Value, target.Email, cancellationToken);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserEmailVerificationSent, request.TargetUserId, request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminRevokeUserSessionsCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminRevokeUserSessionsCommandHandler : IRequestHandler<AdminRevokeUserSessionsCommand, Result<bool, Error>>
{
    private readonly IUserReadRepository _read;
    private readonly IUserWriteRepository _write;
    private readonly IDateTimeProvider _clock;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public AdminRevokeUserSessionsCommandHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IDateTimeProvider clock,
        IAdminAuditLogWriter audit,
        IUnitOfWork uow)
    {
        _read = read;
        _write = write;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminRevokeUserSessionsCommand request, CancellationToken cancellationToken)
    {
        User? target = await _read.GetByIdAsync(request.TargetUserId, cancellationToken: cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        Error? conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        int revoked = await _write.RevokeAllRefreshTokensForUserAsync(request.TargetUserId, _clock.Now, cancellationToken);
        _audit.Append(
            request.ActingAdminUserId,
            AdminAuditActions.UserSessionsRevoked,
            request.TargetUserId,
            request.ActorIpAddress,
            $"tokens_revoked:{revoked}");
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
