using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Books.Commands;
using Domains.Modules.Books.BookAggregate;
using Moq;

namespace Application.Tests.Books;

public sealed class CreateBookCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_adds_book_and_returns_dto()
    {
        var owner = Guid.NewGuid();
        var repo = new Mock<IBookRepository>();
        Book? added = null;
        repo.Setup(r => r.Add(It.IsAny<Book>())).Callback<Book>(b => added = b);

        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(FixedNow);

        var handler = new CreateBookCommandHandler(repo.Object, uow.Object, clock.Object);

        var result = await handler.Handle(
            new CreateBookCommand(owner, "Clean Architecture", "Robert C. Martin", "978-0134494166",
                "A guide to software structure.", 2017),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("Clean Architecture", result.Value.Title);
        Assert.Equal("Robert C. Martin", result.Value.Author);
        Assert.Equal(2017, result.Value.PublishedYear);
        repo.Verify(r => r.Add(It.IsAny<Book>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
