using Application.Contracts.Administration;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Users.Administration;
using Application.Modules.Users.Authentication.Commands.RefreshSession;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Errors.Authentication;
using Shared.Security;

namespace Application.Tests;

/// <summary>
///     Tests for refresh-token rotation with theft detection.
/// </summary>
public sealed class RefreshSessionCommandHandlerTests
{
    private const string RawToken = "0123456789abcdef";
    private static readonly string TokenHash = TokenHasher.Sha256(RawToken);

    [Fact]
    public async Task Handle_returns_invalid_when_token_unknown()
    {
        var write = new Mock<IUserWriteRepository>();
        write.Setup(w => w.GetRefreshTokenByHashForUpdateAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        var handler = BuildHandler(write);

        var result =
            await handler.Handle(new RefreshSessionCommand(RawToken), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidCredentialsError>(result.Error);
    }

    [Fact]
    public async Task Handle_triggers_theft_detection_when_revoked_token_is_replayed()
    {
        var userId = UserId.CreateNew();
        // Build a token, revoke it, then verify the handler detects reuse and mass-revokes.
        var token = RefreshToken.Issue(userId, TokenHash, DateTime.UtcNow.AddDays(7), DateTime.UtcNow, "1.2.3.4");
        token.Revoke();

        var write = new Mock<IUserWriteRepository>();
        write.Setup(w => w.GetRefreshTokenByHashForUpdateAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        write.Setup(w => w.RevokeAllRefreshTokensForUserAsync(
                userId.Value, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var audit = new Mock<IAdminAuditLogWriter>();
        var handler = BuildHandler(write, audit: audit);

        var result =
            await handler.Handle(new RefreshSessionCommand(RawToken, "9.9.9.9"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<InvalidCredentialsError>(result.Error);
        // The defensive revoke-all must have been called exactly once, with the offending user id.
        write.Verify(w => w.RevokeAllRefreshTokensForUserAsync(
            userId.Value, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        // The incident must hit the admin audit log so security tooling can pivot to it.
        audit.Verify(a => a.Append(
            userId.Value,
            AdminAuditActions.UserRefreshTokenReuseDetected,
            userId.Value,
            "9.9.9.9",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Handle_rotates_token_and_issues_pair_on_success()
    {
        var userId = UserId.CreateNew();
        var token = RefreshToken.Issue(userId, TokenHash, DateTime.UtcNow.AddDays(7), DateTime.UtcNow, "1.2.3.4");
        var user = User.CreateNew("F", "L", "u1", "u1@example.com", "hash");

        var write = new Mock<IUserWriteRepository>();
        write.Setup(w => w.GetRefreshTokenByHashForUpdateAsync(TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var read = new Mock<IUserReadRepository>();
        // GetByIdAsync has an optional CacheOptions parameter; Moq's expression-tree setup can't
        // see the default value so we must specify it explicitly via It.IsAny.
        read.Setup(r => r.GetByIdAsync(
                userId.Value,
                It.IsAny<CacheOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var tokens = new Mock<IAuthenticationTokenIssuer>();
        tokens.Setup(t => t.IssueAsync(user, It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new AuthenticationTokenPair("new-access", DateTime.UtcNow.AddMinutes(15), "new-refresh",
                DateTime.UtcNow.AddDays(7)));

        var handler = BuildHandler(write, read, tokens);

        var result =
            await handler.Handle(new RefreshSessionCommand(RawToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-access", result.Value.AccessToken);
        // The original token must be revoked; the rotated pair is the new identity.
        Assert.NotNull(token.RevokedAtUtc);
    }

    private static RefreshSessionCommandHandler BuildHandler(
        Mock<IUserWriteRepository> write,
        Mock<IUserReadRepository>? read = null,
        Mock<IAuthenticationTokenIssuer>? tokens = null,
        Mock<IAdminAuditLogWriter>? audit = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var correlation = new Mock<ICorrelationContext>();
        correlation.SetupGet(c => c.Current).Returns("test-correlation-id");
        return new RefreshSessionCommandHandler(
            (read ?? new Mock<IUserReadRepository>()).Object,
            write.Object,
            (tokens ?? new Mock<IAuthenticationTokenIssuer>()).Object,
            Mock.Of<IUnitOfWork>(),
            clock.Object,
            (audit ?? new Mock<IAdminAuditLogWriter>()).Object,
            correlation.Object,
            NullLogger<RefreshSessionCommandHandler>.Instance);
    }
}