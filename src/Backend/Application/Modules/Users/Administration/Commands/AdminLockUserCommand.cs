using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminLockUserCommand(Guid TargetUserId, string? Reason, Guid ActingAdminUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminLockUserCommandHandler : IRequestHandler<AdminLockUserCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _write;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public AdminLockUserCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminLockUserCommand request, CancellationToken cancellationToken)
    {
        User? target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Lock(request.Reason);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserLocked, request.TargetUserId, request.ActorIpAddress, request.Reason);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminUnlockUserCommand(Guid TargetUserId, Guid ActingAdminUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminUnlockUserCommandHandler : IRequestHandler<AdminUnlockUserCommand, Result<bool, Error>>
{
    private readonly IUserWriteRepository _write;
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;

    public AdminUnlockUserCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminUnlockUserCommand request, CancellationToken cancellationToken)
    {
        User? target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Unlock();
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserUnlocked, request.TargetUserId, request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
