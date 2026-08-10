using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Books.Commands;
using Domains.Modules.Books.BookAggregate;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Books;

public sealed class UpdateBookCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Book NewBook(Guid owner)
    {
        return Book.Create(owner, "Original Title", "Original Author", null, null, null, FixedNow);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_book_missing()
    {
        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Book?)null);

        var handler = new UpdateBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object,
            new Mock<IDateTimeProvider>().Object);

        var result = await handler.Handle(
            new UpdateBookCommand(Guid.NewGuid(), Guid.NewGuid(), "T", "A", null, null, null, 0),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_caller_is_not_owner()
    {
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var book = NewBook(owner);

        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(book.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var handler = new UpdateBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object,
            new Mock<IDateTimeProvider>().Object);

        var result = await handler.Handle(
            new UpdateBookCommand(book.Id.Value, attacker, "T", "A", null, null, null, book.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task Handle_returns_concurrency_conflict_when_row_version_mismatches()
    {
        var owner = Guid.NewGuid();
        var book = NewBook(owner);

        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(book.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var handler = new UpdateBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object,
            new Mock<IDateTimeProvider>().Object);

        var result = await handler.Handle(
            new UpdateBookCommand(book.Id.Value, owner, "T", "A", null, null, null, book.RowVersion + 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ConcurrencyConflictError>(result.Error);
    }

    [Fact]
    public async Task Handle_updates_details_when_owner_and_row_version_match()
    {
        var owner = Guid.NewGuid();
        var book = NewBook(owner);

        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(book.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(FixedNow.AddDays(1));

        var handler = new UpdateBookCommandHandler(repo.Object, uow.Object, clock.Object);

        var result = await handler.Handle(
            new UpdateBookCommand(book.Id.Value, owner, "New Title", "New Author", "123", "desc", 2020,
                book.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Title", result.Value.Title);
        Assert.Equal("New Author", result.Value.Author);
        Assert.Equal(2020, result.Value.PublishedYear);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
