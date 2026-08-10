using Application.Common;
using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Books.Common;
using Application.Modules.Books.Dtos;

namespace Application.Modules.Books.Commands;

/// <summary>
///     Updates an existing book. Only the owner may update it; <see cref="ExpectedRowVersion" /> guards
///     against lost updates when the record changed since it was loaded.
/// </summary>
public sealed record UpdateBookCommand(
    Guid BookId,
    Guid CallerUserId,
    string Title,
    string Author,
    string? Isbn,
    string? Description,
    int? PublishedYear,
    uint ExpectedRowVersion) : IRequest<Result<BookDto, Error>>;

public sealed class UpdateBookCommandValidator : AbstractValidator<UpdateBookCommand>
{
    public UpdateBookCommandValidator()
    {
        RuleFor(x => x.BookId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Isbn).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PublishedYear).InclusiveBetween(0, 9999).When(x => x.PublishedYear.HasValue);
    }
}

public sealed class UpdateBookCommandHandler : IRequestHandler<UpdateBookCommand, Result<BookDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly IBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateBookCommandHandler(IBookRepository repo, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<BookDto, Error>> Handle(UpdateBookCommand request, CancellationToken cancellationToken)
    {
        var book = await _repo.GetByIdForUpdateAsync(request.BookId, cancellationToken);
        if (book is null)
            return new NotFoundError("Book not found.");

        if (book.OwnerUserId != request.CallerUserId)
            return new NotFoundError("Book not found.");

        var conflict = EntityConcurrency.ValidateExpectedVersion(book, request.ExpectedRowVersion);
        if (conflict is not null)
            return conflict;

        book.UpdateDetails(
            request.Title,
            request.Author,
            request.Isbn,
            request.Description,
            request.PublishedYear,
            _clock.Now);

        await _uow.SaveChangesAsync(cancellationToken);

        return BookProjector.ToDto(book);
    }
}
