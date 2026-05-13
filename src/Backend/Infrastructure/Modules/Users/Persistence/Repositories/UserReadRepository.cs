using Application.Common;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Modules.Users.Persistence.Read;
using Domains.UserAggregate;
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
            u => u.Id.Value == id,
            cache,
            IdTtl,
            cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CacheOptions cache = default, CancellationToken cancellationToken = default) =>
        GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ByEmail(email),
            _read.Users,
            u => u.Email == email,
            cache,
            LookupTtl,
            cancellationToken);

    public Task<User?> GetByUserNameAsync(string userName, CacheOptions cache = default, CancellationToken cancellationToken = default) =>
        GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ByUserName(userName),
            _read.Users,
            u => u.UserName == userName,
            cache,
            LookupTtl,
            cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        ExistsAsync(_read.Users, u => u.Email == email, cancellationToken);

    public Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        ExistsAsync(_read.Users, u => u.UserName == userName, cancellationToken);

    public Task<PagedList<User>> GetUsersPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        _read.Users.AsNoTracking().OrderBy(u => u.UserName).ToPagedListAsync(page, pageSize, cancellationToken);
}
