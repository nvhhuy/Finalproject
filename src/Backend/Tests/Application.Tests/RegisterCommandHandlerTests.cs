using Application.Contracts.Email;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.FeatureFlags;
using Application.Contracts.Tenancy;
using Application.Contracts.Transactions;
using Application.Modules.Users.Authentication.Commands.Register;
using Application.Modules.Users.Authentication.Common;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Moq;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Tests;

/// <summary>
/// Unit tests for <see cref="RegisterCommandHandler"/>. We exercise the handler in isolation: the
/// uniqueness checks, the public-registration toggle, the domain event scheduling, and the side
/// effects (persisting through <see cref="IUserWriteRepository"/>, sending the verification email).
/// </summary>
public sealed class RegisterCommandHandlerTests
{
    private const string Email = "newuser@example.com";
    private const string UserName = "newuser";
    private const string Password = "Password123!";

    [Fact]
    public async Task Register_returns_forbidden_when_public_registration_is_disabled()
    {
        var settings = NewFeatureFlags(allowPublicRegister: false);
        var read = new Mock<IUserReadRepository>();
        var write = new Mock<IUserWriteRepository>();
        RegisterCommandHandler handler = BuildHandler(read.Object, write.Object, settings.Object);

        Result<RegistrationResult, Error> result =
            await handler.Handle(NewCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
        read.Verify(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        write.Verify(w => w.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_returns_email_taken_when_email_already_exists()
    {
        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.ExistsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var write = new Mock<IUserWriteRepository>();

        RegisterCommandHandler handler = BuildHandler(read.Object, write.Object, NewFeatureFlags().Object);

        Result<RegistrationResult, Error> result =
            await handler.Handle(NewCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<EmailAlreadyInUseError>(result.Error);
        write.Verify(w => w.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_returns_username_taken_when_username_already_exists()
    {
        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.ExistsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        read.Setup(r => r.ExistsByUserNameAsync(UserName, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var write = new Mock<IUserWriteRepository>();

        RegisterCommandHandler handler = BuildHandler(read.Object, write.Object, NewFeatureFlags().Object);

        Result<RegistrationResult, Error> result =
            await handler.Handle(NewCommand(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<UserNameAlreadyTakenError>(result.Error);
        write.Verify(w => w.AddUser(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_persists_user_and_sends_verification_email_on_success()
    {
        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.ExistsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        read.Setup(r => r.ExistsByUserNameAsync(UserName, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var write = new Mock<IUserWriteRepository>();
        User? addedUser = null;
        write.Setup(w => w.AddUser(It.IsAny<User>())).Callback<User>(u => addedUser = u);

        var emailSender = new Mock<IEmailVerificationSender>();
        var uow = new Mock<IUnitOfWork>();

        RegisterCommandHandler handler = BuildHandler(
            read.Object, write.Object, NewFeatureFlags().Object,
            emailSender: emailSender.Object, uow: uow.Object);

        Result<RegistrationResult, Error> result =
            await handler.Handle(NewCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Email, result.Value.Email);
        Assert.NotNull(addedUser);
        Assert.Equal(Email, addedUser!.Email);
        Assert.Equal(UserName, addedUser.UserName);
        Assert.NotEqual(string.Empty, addedUser.PasswordHash);
        Assert.NotNull(addedUser.RegisteredAtUtc);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(
            e => e.SendVerificationEmailAsync(addedUser.Id.Value, Email, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static RegisterCommand NewCommand() =>
        new("First", "Last", UserName, Email, Password);

    private static Mock<IFeatureFlagService> NewFeatureFlags(bool allowPublicRegister = true)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(s => s.IsEnabledAsync(It.IsAny<string>(), It.IsAny<FeatureFlagContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(allowPublicRegister);
        return mock;
    }

    private static RegisterCommandHandler BuildHandler(
        IUserReadRepository read,
        IUserWriteRepository write,
        IFeatureFlagService featureFlags,
        IEmailVerificationSender? emailSender = null,
        IUnitOfWork? uow = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new RegisterCommandHandler(
            read,
            write,
            new PasswordHasher<User>(),
            featureFlags,
            clock.Object,
            emailSender ?? Mock.Of<IEmailVerificationSender>(),
            uow ?? Mock.Of<IUnitOfWork>(),
            Mock.Of<ITenantEnrollmentService>());
    }
}
