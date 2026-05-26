using Application.Modules.Users.Administration.Commands;
using Domains.UserAggregate;

namespace Application.Tests;

public sealed class AdminUserManagementCommandValidatorTests
{
    [Fact]
    public async Task AdminCreateUser_rejects_role_outside_allowlist()
    {
        var validator = new AdminCreateUserCommandValidator();
        var command = new AdminCreateUserCommand(
            Guid.NewGuid(),
            "A",
            "B",
            "user1",
            "user1@example.com",
            "Password12",
            "Moderator");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdminCreateUserCommand.Role));
    }

    [Fact]
    public async Task AdminCreateUser_accepts_User_and_Admin_roles()
    {
        var validator = new AdminCreateUserCommandValidator();
        var asUser = new AdminCreateUserCommand(
            Guid.NewGuid(), "A", "B", "u1", "u1@e.com", "Password12", UserRoles.User);
        var asAdmin = new AdminCreateUserCommand(
            Guid.NewGuid(), "A", "B", "u2", "u2@e.com", "Password12", UserRoles.Admin);

        Assert.True((await validator.ValidateAsync(asUser)).IsValid);
        Assert.True((await validator.ValidateAsync(asAdmin)).IsValid);
    }

    [Fact]
    public async Task AdminCreateUser_rejects_password_without_uppercase()
    {
        var validator = new AdminCreateUserCommandValidator();
        var command = new AdminCreateUserCommand(
            Guid.NewGuid(), "A", "B", "u1", "u1@e.com", "password12", UserRoles.User);

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdminCreateUserCommand.Password));
    }

    [Fact]
    public async Task AdminResetPassword_rejects_short_password()
    {
        var validator = new AdminResetPasswordCommandValidator();
        var command = new AdminResetPasswordCommand(Guid.NewGuid(), Guid.NewGuid(), "short");

        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
    }
}
