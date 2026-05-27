using Application.Common;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Persistence;
using Application.Modules.Users.Administration.Queries;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Extensions;
using Infrastructure.Persistence.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Users.Persistence.Repositories;

public sealed class UserReadRepository : CachedReadRepositoryBase, IUserReadRepository
{
    private static readonly TimeSpan IdTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LookupTtl = TimeSpan.FromMinutes(3);

    private readonly IApplicationReadDbContext _read;

    public UserReadRepository(IApplicationReadDbContext read, ICacheService cache) : base(cache)
    {
        _read = read;
    }

    public Task<User?> GetByIdAsync(Guid id, CacheOptions cache = default, CancellationToken cancellationToken = default) =>
        GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ById(id),
            _read.Users,
            u => u.Id == UserId.Create(id),
            cache,
            IdTtl,
            cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CacheOptions cache = default, CancellationToken cancellationToken = default)
    {
        // Normalize the input the same way the aggregate normalized it at write-time so the
        // unique index on NormalizedEmail can serve the lookup. Without this, "Foo@x.com" would
        // miss a row stored from "foo@x.com".
        string normalized = NormalizeForLookup(email);
        return GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ByEmail(normalized),
            _read.Users,
            u => u.NormalizedEmail == normalized,
            cache,
            LookupTtl,
            cancellationToken);
    }

    public Task<User?> GetByUserNameAsync(string userName, CacheOptions cache = default, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeForLookup(userName);
        return GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ByUserName(normalized),
            _read.Users,
            u => u.NormalizedUserName == normalized,
            cache,
            LookupTtl,
            cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeForLookup(email);
        return ExistsAsync(_read.Users, u => u.NormalizedEmail == normalized, cancellationToken);
    }

    public Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        string normalized = NormalizeForLookup(userName);
        return ExistsAsync(_read.Users, u => u.NormalizedUserName == normalized, cancellationToken);
    }

    public async Task<PagedList<User>> GetUsersAdminPageAsync(AdminUserListQueryParams p, CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = _read.Users.AsNoTracking().Where(u => u.DeletedAtUtc == null);

        if (!string.IsNullOrWhiteSpace(p.EmailContains))
        {
            // Compare on the normalized column so we hit the unique index. Term is uppercased
            // to match the aggregate's Normalize(...) convention; an .ILike call would also
            // work but forces a sequential scan in production (no functional index exists).
            string term = NormalizeForLookup(p.EmailContains);
            query = query.Where(u => u.NormalizedEmail.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(p.UserNameContains))
        {
            string term = NormalizeForLookup(p.UserNameContains);
            query = query.Where(u => u.NormalizedUserName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            string term = NormalizeForLookup(p.Search);
            query = query.Where(u =>
                u.NormalizedEmail.Contains(term) || u.NormalizedUserName.Contains(term));
        }

        if (p.IsLocked is not null)
            query = query.Where(u => u.IsLocked == p.IsLocked.Value);

        if (!string.IsNullOrWhiteSpace(p.Role))
            query = query.Where(u => u.Role == p.Role);

        string sort = string.IsNullOrWhiteSpace(p.SortBy) ? "username" : p.SortBy.Trim().ToLowerInvariant();
        if (sort is not ("username" or "email" or "registeredat" or "role"))
            sort = "username";

        query = sort switch
        {
            "email" => p.SortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "registeredat" => p.SortDescending ? query.OrderByDescending(u => u.RegisteredAtUtc) : query.OrderBy(u => u.RegisteredAtUtc),
            "role" => p.SortDescending ? query.OrderByDescending(u => u.Role) : query.OrderBy(u => u.Role),
            _ => p.SortDescending ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName)
        };

        return await query.ToPagedListAsync(p.Page, p.PageSize, cancellationToken);
    }

    private static string NormalizeForLookup(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
