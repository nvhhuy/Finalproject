namespace Api.Models.Books;

public sealed record UpdateBookRequest(
    string Title,
    string Author,
    string? Isbn,
    string? Description,
    int? PublishedYear,
    uint RowVersion);
