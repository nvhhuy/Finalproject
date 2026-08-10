using Application.Modules.Books.Dtos;
using Domains.Modules.Books.BookAggregate;

namespace Application.Modules.Books.Common;

public static class BookProjector
{
    public static BookDto ToDto(Book book)
    {
        return new BookDto(
            book.Id.Value,
            book.Title,
            book.Author,
            book.Isbn,
            book.Description,
            book.PublishedYear,
            book.CreatedAtUtc,
            book.UpdatedAtUtc,
            book.RowVersion);
    }
}
