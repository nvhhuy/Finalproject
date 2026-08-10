using Application.Contracts.Modules.Books.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Modules.Books.Common;
using Application.Modules.Books.Dtos;
using Domains.Modules.Books.BookAggregate;

namespace Application.Modules.Books.Commands;

/// <summary>Creates a new book owned by <see cref="OwnerUserId" />.</summary>
public sealed record CreateBookCommand(
    Guid OwnerUserId,
    string Title,
    string Author,
    string? Isbn,
    string? Description,
    int? PublishedYear) : IRequest<Result<BookDto, Error>>;

public sealed class CreateBookCommandValidator : AbstractValidator<CreateBookCommand>
{
    public CreateBookCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Isbn).MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.PublishedYear).InclusiveBetween(0, 9999).When(x => x.PublishedYear.HasValue);
    }
}

public sealed class CreateBookCommandHandler : IRequestHandler<CreateBookCommand, Result<BookDto, Error>>
{
    private readonly IDateTimeProvider _clock;
    private readonly IBookRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateBookCommandHandler(IBookRepository repo, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Result<BookDto, Error>> Handle(CreateBookCommand request, CancellationToken cancellationToken)
    {
        var book = Book.Create(
            request.OwnerUserId,
            request.Title,
            request.Author,
            request.Isbn,
            request.Description,
            request.PublishedYear,
            _clock.Now);

        _repo.Add(book);
        await _uow.SaveChangesAsync(cancellationToken);

        return BookProjector.ToDto(book);
    }
}
