using Application.Contracts.Persistence;
using Application.Modules.Books.Dtos;
using Domains.Modules.Books.BookAggregate;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Books.Queries;

/// <summary>
///     Fetch one book by id, on behalf of <see cref="CallerUserId" />. Returns <c>NotFound</c> (never
///     <c>Forbidden</c>) when the book exists but belongs to someone else, so enumeration cannot
///     distinguish "no such id" from "not mine".
/// </summary>
public sealed record GetBookQuery(Guid BookId, Guid CallerUserId) : IRequest<Result<BookDto, Error>>;

public sealed class GetBookQueryHandler : IRequestHandler<GetBookQuery, Result<BookDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public GetBookQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<BookDto, Error>> Handle(GetBookQuery request, CancellationToken cancellationToken)
    {
        var book = await _read.Books
            .AsNoTracking()
            .Where(b => b.Id == BookId.Create(request.BookId) && b.OwnerUserId == request.CallerUserId)
            .Select(b => new BookDto(
                b.Id.Value,
                b.Title,
                b.Author,
                b.Isbn,
                b.Description,
                b.PublishedYear,
                b.CreatedAtUtc,
                b.UpdatedAtUtc,
                b.RowVersion))
            .FirstOrDefaultAsync(cancellationToken);

        return book is null ? new NotFoundError("Book not found.") : book;
    }
}
