using Application.Common;
using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminLockUserCommand(
    Guid TargetUserId,
    string? Reason,
    Guid ActingAdminUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminLockUserCommandHandler : IRequestHandler<AdminLockUserCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminLockUserCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminLockUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.Lock(request.Reason);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserLocked, request.TargetUserId,
            request.ActorIpAddress, request.Reason);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminUnlockUserCommand(
    Guid TargetUserId,
    Guid ActingAdminUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminUnlockUserCommandHandler : IRequestHandler<AdminUnlockUserCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminUnlockUserCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminUnlockUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.Unlock();
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserUnlocked, request.TargetUserId,
            request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}