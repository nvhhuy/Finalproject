using Application.Contracts.Administration;
using Application.Contracts.Modules.Users.Persistence;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminLockUserCommand(Guid TargetUserId, string? Reason, Guid ActingAdminUserId, string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>;

public sealed class AdminLockUserCommandHandler : IRequestHandler<AdminLockUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IAdminAuditLogWriter _audit;

    public AdminLockUserCommandHandler(IUserRespository users, IAdminAuditLogWriter audit)
    {
        _users = users;
        _audit = audit;
    }

    public async Task<Result<bool, Error>> Handle(AdminLockUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Lock(request.Reason);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserLocked, request.TargetUserId, request.ActorIpAddress, request.Reason);
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminUnlockUserCommand(Guid TargetUserId, Guid ActingAdminUserId, string? ActorIpAddress = null) : IRequest<Result<bool, Error>>;

public sealed class AdminUnlockUserCommandHandler : IRequestHandler<AdminUnlockUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IAdminAuditLogWriter _audit;

    public AdminUnlockUserCommandHandler(IUserRespository users, IAdminAuditLogWriter audit)
    {
        _users = users;
        _audit = audit;
    }

    public async Task<Result<bool, Error>> Handle(AdminUnlockUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Unlock();
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserUnlocked, request.TargetUserId, request.ActorIpAddress, null);
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}
