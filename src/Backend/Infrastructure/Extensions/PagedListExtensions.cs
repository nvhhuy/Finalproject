using Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Extensions;

public static class PagedListExtensions
{
    public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        where T : class
    {
        int count = await source.CountAsync(cancellationToken);
        List<T> items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedList<T>(items, count, pageNumber, pageSize);
    }
}
