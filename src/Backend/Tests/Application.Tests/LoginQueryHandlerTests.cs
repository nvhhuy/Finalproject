using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Modules.Users.Authentication.Queries;
using Domains.UserAggregate;
using Microsoft.AspNetCore.Identity;
using Moq;
using Shared.Errors.Authentication;

namespace Application.Tests;

public sealed class LoginQueryHandlerTests
{
    [Fact]
    public async Task Login_rejects_unverified_email()
    {
        User user = User.CreateNew("A", "B", "u1", "a@b.com", "plain");
        var hasher = new PasswordHasher<User>();
        string hash = hasher.HashPassword(user, "Password123!");
        user.UpdatePassword(hash);

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByEmailAsync("a@b.com", It.IsAny<CacheOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher<User>>();
        passwordHasher
            .Setup(h => h.VerifyHashedPassword(user, hash, "Password123!"))
            .Returns(PasswordVerificationResult.Success);

        var handler = new LoginQueryHandler(
            read.Object,
            Mock.Of<IAuthenticationTokenIssuer>(),
            passwordHasher.Object,
            Mock.Of<ICacheService>());

        var result = await handler.Handle(new LoginQuery("a@b.com", "Password123!"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<EmailNotVerifiedError>(result.Error);
    }
}
