using Domains.Modules.Books.BookAggregate;

namespace Application.Contracts.Modules.Books.Persistence;

/// <summary>
///     Write-side repository for <see cref="Book" />. The aggregate is small and has no caching, so a
///     single combined read/write repository (Media-module style) keeps the layout simple.
/// </summary>
public interface IBookRepository
{
    /// <summary>Loads a tracked book for state-changing operations.</summary>
    Task<Book?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Book book);

    void Remove(Book book);
}
