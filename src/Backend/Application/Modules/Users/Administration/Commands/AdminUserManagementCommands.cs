using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Modules.Users.Persistence.Read;
using Domains.UserAggregate;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Modules.Users.Administration.Commands;

public sealed record AdminCreateUserCommand(
    Guid ActingAdminUserId,
    string FirstName,
    string LastName,
    string UserName,
    string Email,
    string Password,
    string Role = UserRoles.User) : IRequest<Result<Guid, Error>>;

public sealed class AdminCreateUserCommandHandler : IRequestHandler<AdminCreateUserCommand, Result<Guid, Error>>
{
    private readonly IUserRespository _users;
    private readonly IUserReadRepository _read;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminCreateUserCommandHandler(IUserRespository users, IUserReadRepository read, IPasswordHasher<User> passwordHasher)
    {
        _users = users;
        _read = read;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid, Error>> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validation = await request.ValidateAsync(new AdminCreateUserCommandValidator(), cancellationToken);
        if (validation.IsFailure)
            return validation.Error;

        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        if (await _read.ExistsByEmailAsync(request.Email, cancellationToken))
            return new EmailAlreadyInUseError();
        if (await _read.ExistsByUserNameAsync(request.UserName, cancellationToken))
            return new UserNameAlreadyTakenError();

        User user = User.CreateNew(request.FirstName, request.LastName, request.UserName, request.Email, request.Password);
        user.AssignRole(request.Role);
        user.UpdatePassword(_passwordHasher.HashPassword(user, request.Password));

        _users.AddUser(user);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(user.Id.Value, user.Email, user.UserName, cancellationToken);
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
    string Role) : IRequest<Result<bool, Error>>;

public sealed class AdminUpdateUserCommandHandler : IRequestHandler<AdminUpdateUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;

    public AdminUpdateUserCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validation = await request.ValidateAsync(new AdminUpdateUserCommandValidator(), cancellationToken);
        if (validation.IsFailure)
            return validation.Error;

        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        User? emailOwner = await _users.GetUserByEmailAsync(request.Email);
        if (emailOwner is not null && emailOwner.Id.Value != request.TargetUserId)
            return new EmailAlreadyInUseError();

        User? usernameOwner = await _users.GetUserByUserNameAsync(request.UserName);
        if (usernameOwner is not null && usernameOwner.Id.Value != request.TargetUserId)
            return new UserNameAlreadyTakenError();

        target.UpdateProfile(request.FirstName, request.LastName, request.UserName, request.Email);
        target.AssignRole(request.Role);
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminDeleteUserCommand(Guid ActingAdminUserId, Guid TargetUserId) : IRequest<Result<bool, Error>>;

public sealed class AdminDeleteUserCommandHandler : IRequestHandler<AdminDeleteUserCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    public AdminDeleteUserCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(AdminDeleteUserCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.MarkDeleted();
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResetTwoFactorCommand(Guid ActingAdminUserId, Guid TargetUserId) : IRequest<Result<bool, Error>>;

public sealed class AdminResetTwoFactorCommandHandler : IRequestHandler<AdminResetTwoFactorCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    public AdminResetTwoFactorCommandHandler(IUserRespository users) => _users = users;

    public async Task<Result<bool, Error>> Handle(AdminResetTwoFactorCommand request, CancellationToken cancellationToken)
    {
        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.DisableTwoFactor();
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}

public sealed record AdminResetPasswordCommand(Guid ActingAdminUserId, Guid TargetUserId, string NewPassword) : IRequest<Result<bool, Error>>;

public sealed class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminResetPasswordCommandHandler(IUserRespository users, IPasswordHasher<User> passwordHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<bool, Error>> Handle(AdminResetPasswordCommand request, CancellationToken cancellationToken)
    {
        Result<bool, ValidationError> validation = await request.ValidateAsync(new AdminResetPasswordCommandValidator(), cancellationToken);
        if (validation.IsFailure)
            return validation.Error;

        User? admin = await _users.GetUserByIdAsync(request.ActingAdminUserId, cancellationToken);
        if (admin is null || admin.Role != UserRoles.Admin)
            return new ForbiddenError("Administrator privileges are required.");

        User? target = await _users.GetUserByIdForUpdateAsync(request.TargetUserId, cancellationToken);
        if (target is null)
            return new NotFoundError("User was not found.");

        target.UpdatePassword(_passwordHasher.HashPassword(target, request.NewPassword));
        await _users.SaveChangesAsync(cancellationToken);
        await _users.InvalidateUserCacheAsync(target.Id.Value, target.Email, target.UserName, cancellationToken);
        return true;
    }
}
