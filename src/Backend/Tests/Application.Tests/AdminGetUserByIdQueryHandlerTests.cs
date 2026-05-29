using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Modules.Users.Administration.Queries;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Moq;
using Shared.Errors.General;

namespace Application.Tests;

public sealed class AdminGetUserByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_returns_not_found_when_user_missing()
    {
        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new AdminGetUserByIdQueryHandler(read.Object);

        var result = await handler.Handle(new AdminGetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_detail_for_active_user()
    {
        var id = Guid.NewGuid();
        var user = User.Create(UserId.Create(id), "Ada", "Lovelace", "ada", "ada@example.com", "hash");

        var read = new Mock<IUserReadRepository>();
        read.Setup(r => r.GetByIdAsync(id, default, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new AdminGetUserByIdQueryHandler(read.Object);

        var result = await handler.Handle(new AdminGetUserByIdQuery(id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Ada", result.Value.FirstName);
        Assert.Equal("Lovelace", result.Value.LastName);
        Assert.Equal("ada@example.com", result.Value.Email);
    }
}