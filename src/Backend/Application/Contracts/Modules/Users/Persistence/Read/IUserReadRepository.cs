using Application.Common;
using Application.Contracts.Caching;
using Domains.UserAggregate;

using Application.Modules.Users.Administration.Queries;

namespace Application.Contracts.Modules.Users.Persistence.Read;

public interface IUserReadRepository
{
    Task<User?> GetByIdAsync(Guid id, CacheOptions cache = default, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CacheOptions cache = default, CancellationToken cancellationToken = default);
    Task<User?> GetByUserNameAsync(string userName, CacheOptions cache = default, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ExistsByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<PagedList<User>> GetUsersPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedList<User>> GetUsersAdminPageAsync(AdminUserListQueryParams query, CancellationToken cancellationToken = default);
}
