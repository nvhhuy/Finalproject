namespace Api.Models.Books;

public sealed record CreateBookRequest(
    string Title,
    string Author,
    string? Isbn,
    string? Description,
    int? PublishedYear);
