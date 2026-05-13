using Domains.UserAggregate;

namespace Application.Contracts.Modules.Users.Persistence.Write;

public interface IUserWriteRepository
{
    Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    void AddUser(User user);
    void AddExternalLogin(ExternalUserLogin login);
    Task<RefreshToken?> GetRefreshTokenByHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task InvalidateUserCacheAsync(Guid userId, string? email = null, string? userName = null, CancellationToken cancellationToken = default);
}
