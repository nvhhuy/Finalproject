using Application.Contracts.Modules.Books.Persistence;
using Domains.Modules.Books.BookAggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Books.Persistence;

/// <summary>Write repository for <see cref="Book" />. Reads for state changes hit the write context.</summary>
public sealed class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _write;

    public BookRepository(ApplicationDbContext write)
    {
        _write = write;
    }

    public Task<Book?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _write.Books.FirstOrDefaultAsync(b => b.Id == BookId.Create(id), cancellationToken);
    }

    public void Add(Book book)
    {
        _write.Books.Add(book);
    }

    public void Remove(Book book)
    {
        _write.Books.Remove(book);
    }
}
