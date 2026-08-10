using Application.Contracts.Persistence;
using Application.Modules.Books.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Application.Modules.Books.Queries;

/// <summary>Paged list of the caller's own books, optionally filtered by title/author search text.</summary>
public sealed record ListMyBooksQuery(
    Guid CallerUserId,
    int Page,
    int PageSize,
    string? Search) : IRequest<Result<BookListDto, Error>>;

public sealed class ListMyBooksQueryHandler : IRequestHandler<ListMyBooksQuery, Result<BookListDto, Error>>
{
    private readonly IApplicationReadDbContext _read;

    public ListMyBooksQueryHandler(IApplicationReadDbContext read)
    {
        _read = read;
    }

    public async Task<Result<BookListDto, Error>> Handle(ListMyBooksQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200)
            return new Error("BadRequest", 400, "Page must be at least 1 and page size between 1 and 200.");

        var q = _read.Books.AsNoTracking().Where(b => b.OwnerUserId == request.CallerUserId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(b => b.Title.ToUpper().Contains(s.ToUpper()) || b.Author.ToUpper().Contains(s.ToUpper()));
        }

        var total = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(b => b.UpdatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
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
            .ToListAsync(cancellationToken);

        return new BookListDto(items, total, request.Page, request.PageSize);
    }
}
