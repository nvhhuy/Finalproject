using Application.Contracts.Authentication;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication.Common;
using Application.Modules.Users.Authentication.Queries;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Moq;
using Shared.Common;
using Shared.Errors;
using Shared.Errors.Authentication;

namespace Application.Tests;

public sealed class LoginQueryHandlerTests
{
    [Fact]
    public async Task Login_rejects_unverified_email()
    {
        User user = NewUser();
        string hash = HashPassword(user, "Password123!");
        user.UpdatePasswordHash(hash);

        LoginQueryHandler handler = BuildHandler(
            user,
            hash,
            "Password123!",
            verifyResult: PasswordVerificationResult.Success);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery(user.Email, "Password123!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<EmailNotVerifiedError>(result.Error);
    }

    [Fact]
    public async Task Login_returns_temporary_lock_error_when_tracker_reports_active_lockout()
    {
        User user = NewUser();

        var tracker = new Mock<IFailedLoginTracker>();
        tracker.Setup(t => t.GetLockoutRemainingAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TimeSpan.FromSeconds(120));

        LoginQueryHandler handler = BuildHandler(user, "irrelevant-hash", "irrelevant", PasswordVerificationResult.Success, tracker.Object);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery(user.Email, "any"), CancellationToken.None);

        Assert.True(result.IsFailure);
        AccountTemporarilyLockedError locked = Assert.IsType<AccountTemporarilyLockedError>(result.Error);
        Assert.Equal(TimeSpan.FromSeconds(120), locked.RetryAfter);
    }

    [Fact]
    public async Task Login_registers_failure_when_password_is_wrong()
    {
        User user = NewUserVerified();
        string hash = HashPassword(user, "Correct!");
        user.UpdatePasswordHash(hash);

        var tracker = new Mock<IFailedLoginTracker>();
        tracker.Setup(t => t.GetLockoutRemainingAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan?)null);
        tracker.Setup(t => t.RegisterFailureAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        LoginQueryHandler handler = BuildHandler(user, hash, "Wrong!", PasswordVerificationResult.Failed, tracker.Object);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery(user.Email, "Wrong!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidCredentialsError>(result.Error);
        tracker.Verify(t => t.RegisterFailureAsync(user.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_promotes_to_lock_error_when_failure_triggers_threshold()
    {
        User user = NewUserVerified();
        string hash = HashPassword(user, "Correct!");
        user.UpdatePasswordHash(hash);

        var tracker = new Mock<IFailedLoginTracker>();
        TimeSpan? lockoutAfterFailure = TimeSpan.FromSeconds(60);
        tracker.SetupSequence(t => t.GetLockoutRemainingAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan?)null)
            .ReturnsAsync(lockoutAfterFailure);
        tracker.Setup(t => t.RegisterFailureAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        LoginQueryHandler handler = BuildHandler(user, hash, "Wrong!", PasswordVerificationResult.Failed, tracker.Object);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery(user.Email, "Wrong!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        AccountTemporarilyLockedError locked = Assert.IsType<AccountTemporarilyLockedError>(result.Error);
        Assert.Equal(lockoutAfterFailure, locked.RetryAfter);
    }

    [Fact]
    public async Task Login_resets_failure_counter_on_successful_password()
    {
        User user = NewUserVerified();
        string hash = HashPassword(user, "Correct!");
        user.UpdatePasswordHash(hash);

        var tracker = new Mock<IFailedLoginTracker>();
        tracker.Setup(t => t.GetLockoutRemainingAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan?)null);

        var issuer = new Mock<IAuthenticationTokenIssuer>();
        issuer.Setup(i => i.IssueAsync(user, It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new AuthenticationTokenPair(
                AccessToken: "access",
                AccessTokenExpiresUtc: DateTime.UtcNow.AddMinutes(15),
                RefreshToken: "refresh",
                RefreshTokenExpiresUtc: DateTime.UtcNow.AddDays(7)));

        LoginQueryHandler handler = BuildHandler(user, hash, "Correct!", PasswordVerificationResult.Success, tracker.Object, issuer.Object);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery(user.Email, "Correct!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        tracker.Verify(t => t.ResetAsync(user.Email, It.IsAny<CancellationToken>()), Times.Once);
        tracker.Verify(t => t.RegisterFailureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_registers_failure_when_user_does_not_exist()
    {
        var tracker = new Mock<IFailedLoginTracker>();
        tracker.Setup(t => t.GetLockoutRemainingAsync("ghost@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan?)null);

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByEmailAsync("ghost@example.com", It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new LoginQueryHandler(
            read.Object,
            Mock.Of<IAuthenticationTokenIssuer>(),
            Mock.Of<IPasswordHasher<User>>(),
            Mock.Of<ICacheService>(),
            tracker.Object);

        Result<AuthenticationResponse, Error> result =
            await handler.Handle(new LoginQuery("ghost@example.com", "x"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidCredentialsError>(result.Error);
        tracker.Verify(t => t.RegisterFailureAsync("ghost@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static User NewUser() => User.CreateForLocalLoginPendingHash("A", "B", "u1", "a@b.com");

    private static User NewUserVerified()
    {
        User u = NewUser();
        u.MarkEmailVerified(DateTime.UtcNow);
        return u;
    }

    private static string HashPassword(User user, string plaintext)
    {
        var hasher = new PasswordHasher<User>();
        return hasher.HashPassword(user, plaintext);
    }

    private static LoginQueryHandler BuildHandler(
        User user,
        string storedHash,
        string suppliedPassword,
        PasswordVerificationResult verifyResult,
        IFailedLoginTracker? tracker = null,
        IAuthenticationTokenIssuer? issuer = null)
    {
        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher<User>>();
        passwordHasher
            .Setup(h => h.VerifyHashedPassword(user, storedHash, suppliedPassword))
            .Returns(verifyResult);

        return new LoginQueryHandler(
            read.Object,
            issuer ?? Mock.Of<IAuthenticationTokenIssuer>(),
            passwordHasher.Object,
            Mock.Of<ICacheService>(),
            tracker ?? Mock.Of<IFailedLoginTracker>());
    }
}
