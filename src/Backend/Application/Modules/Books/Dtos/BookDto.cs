namespace Application.Modules.Books.Dtos;

public sealed record BookDto(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Description,
    int? PublishedYear,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint RowVersion);

public sealed record BookListDto(IReadOnlyList<BookDto> Items, int TotalCount, int Page, int PageSize);
