using Application.Contracts.Administration;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Users.Administration.Commands;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Moq;
using Shared.Errors.Authentication;
using Shared.Errors.General;

namespace Application.Tests;

public sealed class ImpersonateUserCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TargetId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Impersonate_returns_forbidden_when_target_is_admin()
    {
        var target = User.Create(UserId.Create(TargetId), "A", "B", "admin2", "admin2@e.com", "hash");
        target.AssignRole(UserRoles.Admin);

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByIdAsync(TargetId, default, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var handler = BuildHandler(read.Object);

        var result = await handler.Handle(
            new ImpersonateUserCommand(TargetId, AdminId, 0),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task Impersonate_returns_disabled_when_target_cannot_sign_in()
    {
        var target = User.Create(UserId.Create(TargetId), "A", "B", "locked", "locked@e.com", "hash");
        target.Lock("test");

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByIdAsync(TargetId, default, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var handler = BuildHandler(read.Object);

        var result = await handler.Handle(
            new ImpersonateUserCommand(TargetId, AdminId, 0),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<AccountDisabledError>(result.Error);
    }

    [Fact]
    public async Task Impersonate_issues_tokens_for_valid_target()
    {
        var target = User.Create(UserId.Create(TargetId), "A", "B", "user1", "user1@e.com", "hash");

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByIdAsync(TargetId, default, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var tokens = new Mock<IAuthenticationTokenIssuer>();
        tokens.Setup(t => t.IssueAsync(
                target,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>(),
                AdminId))
            .ReturnsAsync(new AuthenticationTokenPair("access", DateTime.UtcNow.AddHours(1), "refresh",
                DateTime.UtcNow.AddDays(7)));

        var handler = BuildHandler(read.Object, tokens.Object);

        var result = await handler.Handle(
            new ImpersonateUserCommand(TargetId, AdminId, 0),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access", result.Value.AccessToken);
    }

    private static ImpersonateUserCommandHandler BuildHandler(
        IUserReadRepository read,
        IAuthenticationTokenIssuer? tokens = null)
    {
        var audit = new Mock<IAdminAuditLogWriter>();
        var uow = new Mock<IUnitOfWork>();
        return new ImpersonateUserCommandHandler(
            read,
            tokens ?? Mock.Of<IAuthenticationTokenIssuer>(),
            audit.Object,
            uow.Object);
    }
}