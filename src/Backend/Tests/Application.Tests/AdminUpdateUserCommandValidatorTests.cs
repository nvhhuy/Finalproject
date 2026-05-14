using Application.Modules.Users.Administration.Commands;
using Domains.UserAggregate;

namespace Application.Tests;

public sealed class AdminUpdateUserCommandValidatorTests
{
    private readonly AdminUpdateUserCommandValidator _validator = new();

    [Fact]
    public async Task AdminUpdateUser_rejects_invalid_role()
    {
        var command = new AdminUpdateUserCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A",
            "B",
            "u1",
            "u1@e.com",
            "Moderator");

        var result = await _validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdminUpdateUserCommand.Role));
    }

    [Fact]
    public async Task AdminUpdateUser_accepts_User_and_Admin_roles()
    {
        Guid adminId = Guid.NewGuid();
        Guid targetId = Guid.NewGuid();
        var asUser = new AdminUpdateUserCommand(adminId, targetId, "A", "B", "u1", "u1@e.com", UserRoles.User);
        var asAdmin = new AdminUpdateUserCommand(adminId, targetId, "A", "B", "u2", "u2@e.com", UserRoles.Admin);

        Assert.True((await _validator.ValidateAsync(asUser)).IsValid);
        Assert.True((await _validator.ValidateAsync(asAdmin)).IsValid);
    }
}
