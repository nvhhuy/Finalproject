using Application.Contracts.Modules.Users.Persistence;
using Domains.UserAggregate;
using MediatR;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminLockUserCommand(Guid TargetUserId, string? Reason, Guid ActingAdminUserId)
    : IRequest<Result<bool, Error>>;

public sealed class AdminLockUserCommandHandler : IRequestHandler<AdminLockUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;

    public AdminLockUserCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(AdminLockUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Lock(request.Reason);
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public sealed record AdminUnlockUserCommand(Guid TargetUserId, Guid ActingAdminUserId) : IRequest<Result<bool, Error>>;

public sealed class AdminUnlockUserCommandHandler : IRequestHandler<AdminUnlockUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;

    public AdminUnlockUserCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(AdminUnlockUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.Unlock();
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }
}
