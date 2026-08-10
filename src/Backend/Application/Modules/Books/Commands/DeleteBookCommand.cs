using Application.Common;
using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Transactions;

namespace Application.Modules.Books.Commands;

/// <summary>Removes a book. Only the owner may delete it; guarded by optimistic concurrency.</summary>
public sealed record DeleteBookCommand(Guid BookId, Guid CallerUserId, uint ExpectedRowVersion)
    : IRequest<Result<bool, Error>>;

public sealed class DeleteBookCommandValidator : AbstractValidator<DeleteBookCommand>
{
    public DeleteBookCommandValidator()
    {
        RuleFor(x => x.BookId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}

public sealed class DeleteBookCommandHandler : IRequestHandler<DeleteBookCommand, Result<bool, Error>>
{
    private readonly IBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeleteBookCommandHandler(IBookRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(DeleteBookCommand request, CancellationToken cancellationToken)
    {
        var book = await _repo.GetByIdForUpdateAsync(request.BookId, cancellationToken);
        if (book is null)
            return new NotFoundError("Book not found.");

        if (book.OwnerUserId != request.CallerUserId)
            return new NotFoundError("Book not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(book, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        _repo.Remove(book);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }
}
