using Domains.Common;

namespace Domains.Modules.Books.BookAggregate;

/// <summary>
///     Aggregate root for a single book owned by the authenticated user who created it. Books are
///     private per-owner: every read/write path filters by <see cref="OwnerUserId" /> so users can
///     only see and mutate their own catalog entries.
/// </summary>
public sealed class Book : AggregateRoot<BookId>
{
    private Book()
    {
    }

    private Book(
        BookId id,
        Guid ownerUserId,
        string title,
        string author,
        string? isbn,
        string? description,
        int? publishedYear,
        DateTime utcNow) : base(id)
    {
        OwnerUserId = ownerUserId;
        Title = title;
        Author = author;
        Isbn = isbn;
        Description = description;
        PublishedYear = publishedYear;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public Guid OwnerUserId { get; }
    public string Title { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public string? Isbn { get; private set; }
    public string? Description { get; private set; }
    public int? PublishedYear { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Book Create(
        Guid ownerUserId,
        string title,
        string author,
        string? isbn,
        string? description,
        int? publishedYear,
        DateTime utcNow)
    {
        return Create(BookId.CreateNew(), ownerUserId, title, author, isbn, description, publishedYear, utcNow);
    }

    public static Book Create(
        BookId id,
        Guid ownerUserId,
        string title,
        string author,
        string? isbn,
        string? description,
        int? publishedYear,
        DateTime utcNow)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("Owner user id is required.", nameof(ownerUserId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author is required.", nameof(author));
        if (publishedYear is { } year && (year < 0 || year > utcNow.Year + 1))
            throw new ArgumentOutOfRangeException(nameof(publishedYear), "Published year is out of range.");

        return new Book(id, ownerUserId, title.Trim(), author.Trim(), isbn?.Trim(), description?.Trim(),
            publishedYear, utcNow);
    }

    public void UpdateDetails(
        string title,
        string author,
        string? isbn,
        string? description,
        int? publishedYear,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(author))
            throw new ArgumentException("Author is required.", nameof(author));
        if (publishedYear is { } year && (year < 0 || year > utcNow.Year + 1))
            throw new ArgumentOutOfRangeException(nameof(publishedYear), "Published year is out of range.");

        Title = title.Trim();
        Author = author.Trim();
        Isbn = isbn?.Trim();
        Description = description?.Trim();
        PublishedYear = publishedYear;
        UpdatedAtUtc = utcNow;
    }
}
