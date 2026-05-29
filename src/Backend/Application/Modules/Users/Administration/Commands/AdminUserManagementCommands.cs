using Application.Common;
using Application.Contracts.Administration;
using Application.Contracts.Authorization;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Shared.Errors.Authentication;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminCreateUserCommand(
    Guid ActingAdminUserId,
    string FirstName,
    string LastName,
    string UserName,
    string Email,
    string Password,
    string Role = UserRoles.User,
    string? ActorIpAddress = null) : IRequest<Result<Guid, Error>>, IRequireAdmin;

public sealed class AdminCreateUserCommandHandler : IRequestHandler<AdminCreateUserCommand, Result<Guid, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUserReadRepository _read;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminCreateUserCommandHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IPasswordHasher<User> passwordHasher,
        IDateTimeProvider clock,
        IAdminAuditLogWriter audit,
        IUnitOfWork uow)
    {
        _read = read;
        _write = write;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<Guid, Error>> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await _read.ExistsByEmailAsync(request.Email, cancellationToken))
            return new EmailAlreadyInUseError();
        if (await _read.ExistsByUserNameAsync(request.UserName, cancellationToken))
            return new UserNameAlreadyTakenError();

        var user = User.CreateForLocalLoginPendingHash(request.FirstName, request.LastName, request.UserName,
            request.Email);
        user.AssignRole(request.Role);
        user.SetInitialPasswordHash(_passwordHasher.HashPassword(user, request.Password));
        user.MarkRegisteredAt(_clock.Now);
        user.MarkEmailVerified(_clock.Now);

        _write.AddUser(user);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserCreated, user.Id.Value, request.ActorIpAddress,
            null);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
        return user.Id.Value;
    }
}

public sealed record AdminUpdateUserCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    string FirstName,
    string LastName,
    string UserName,
    string Email,
    string Role,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null) : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminUpdateUserCommandHandler : IRequestHandler<AdminUpdateUserCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUserReadRepository _read;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminUpdateUserCommandHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IAdminAuditLogWriter audit,
        IUnitOfWork uow)
    {
        _read = read;
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        var emailOwner = await _read.GetByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (emailOwner is not null && emailOwner.Id.Value != request.TargetUserId)
            return new EmailAlreadyInUseError();

        var usernameOwner = await _read.GetByUserNameAsync(request.UserName, cancellationToken: cancellationToken);
        if (usernameOwner is not null && usernameOwner.Id.Value != request.TargetUserId)
            return new UserNameAlreadyTakenError();

        var previousRole = target.Role;
        target.UpdateProfile(request.FirstName, request.LastName, request.UserName, request.Email);
        target.AssignRole(request.Role);
        var details = previousRole != request.Role ? $"role:{previousRole}->{request.Role}" : null;
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserUpdated, request.TargetUserId,
            request.ActorIpAddress, details);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminDeleteUserCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminDeleteUserCommandHandler : IRequestHandler<AdminDeleteUserCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminDeleteUserCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminDeleteUserCommand request, CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.MarkDeleted();
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserDeleted, request.TargetUserId,
            request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResetTwoFactorCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null)
    : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminResetTwoFactorCommandHandler : IRequestHandler<AdminResetTwoFactorCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminResetTwoFactorCommandHandler(IUserWriteRepository write, IAdminAuditLogWriter audit, IUnitOfWork uow)
    {
        _write = write;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminResetTwoFactorCommand request,
        CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.DisableTwoFactor();
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserTwoFactorReset, request.TargetUserId,
            request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResetPasswordCommand(
    Guid ActingAdminUserId,
    Guid TargetUserId,
    string NewPassword,
    uint ExpectedRowVersion,
    string? ActorIpAddress = null) : IRequest<Result<bool, Error>>, IRequireAdmin;

public sealed class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Result<bool, Error>>
{
    private readonly IAdminAuditLogWriter _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUnitOfWork _uow;
    private readonly IUserWriteRepository _write;

    public AdminResetPasswordCommandHandler(
        IUserWriteRepository write,
        IPasswordHasher<User> passwordHasher,
        IDateTimeProvider clock,
        IAdminAuditLogWriter audit,
        IUnitOfWork uow)
    {
        _write = write;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(AdminResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var target = await _write.GetByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(target, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        target.UpdatePasswordHash(_passwordHasher.HashPassword(target, request.NewPassword));
        await _write.RevokeAllRefreshTokensForUserAsync(request.TargetUserId, _clock.Now, cancellationToken);
        _audit.Append(request.ActingAdminUserId, AdminAuditActions.UserPasswordReset, request.TargetUserId,
            request.ActorIpAddress, null);
        await _uow.SaveChangesAsync(cancellationToken);
        await _write.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}