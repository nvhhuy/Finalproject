using Domains.UserAggregate;

namespace Application.Contracts.Modules.Users.Persistence;

/// <summary>Aggregate port for user persistence (facade over CQRS read/write repositories).</summary>
public interface IUserRespository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByUserNameAsync(string userName);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked entity from the write database for commands that mutate and call SaveChanges.</summary>
    Task<User?> GetUserByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    void AddUser(User user);
    void AddExternalLogin(ExternalUserLogin login);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears read-side cache entries for this user (safe after insert or update).</summary>
    Task InvalidateUserCacheAsync(Guid userId, string? email = null, string? userName = null, CancellationToken cancellationToken = default);
}
