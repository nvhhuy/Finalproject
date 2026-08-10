using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Transactions;
using Application.Modules.Books.Commands;
using Domains.Modules.Books.BookAggregate;
using Moq;
using Shared.Errors.General;

namespace Application.Tests.Books;

public sealed class DeleteBookCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Book NewBook(Guid owner)
    {
        return Book.Create(owner, "Title", "Author", null, null, null, FixedNow);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_book_missing()
    {
        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Book?)null);

        var handler = new DeleteBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new DeleteBookCommand(Guid.NewGuid(), Guid.NewGuid(), 0),
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

        var handler = new DeleteBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new DeleteBookCommand(book.Id.Value, attacker, book.RowVersion),
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

        var handler = new DeleteBookCommandHandler(repo.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(new DeleteBookCommand(book.Id.Value, owner, book.RowVersion + 1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.IsType<ConcurrencyConflictError>(result.Error);
    }

    [Fact]
    public async Task Handle_removes_book_when_owner_and_row_version_match()
    {
        var owner = Guid.NewGuid();
        var book = NewBook(owner);

        var repo = new Mock<IBookRepository>();
        repo.Setup(r => r.GetByIdForUpdateAsync(book.Id.Value, It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var uow = new Mock<IUnitOfWork>();
        var handler = new DeleteBookCommandHandler(repo.Object, uow.Object);

        var result = await handler.Handle(new DeleteBookCommand(book.Id.Value, owner, book.RowVersion),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        repo.Verify(r => r.Remove(book), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
