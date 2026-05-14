using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Modules.Users.Persistence.Read;
using Application.Contracts.Modules.Users.Persistence.Write;
using Domains.UserAggregate;

namespace Infrastructure.Modules.Users.Persistence.Repositories;

/// <summary>Facade implementing the legacy user persistence port using CQRS read/write repositories.</summary>
public sealed class UserRepository : IUserRespository
{
    private readonly IUserReadRepository _read;
    private readonly IUserWriteRepository _write;

    public UserRepository(IUserReadRepository read, IUserWriteRepository write)
    {
        _read = read;
        _write = write;
    }

    public Task<User?> GetUserByEmailAsync(string email) => _read.GetByEmailAsync(email);

    public Task<User?> GetUserByUserNameAsync(string userName) => _read.GetByUserNameAsync(userName);

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _read.GetByIdAsync(id, cancellationToken: cancellationToken);

    public Task<User?> GetUserByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        _write.GetByIdForUpdateAsync(id, cancellationToken);

    public void AddUser(User user) => _write.AddUser(user);

    public void AddExternalLogin(ExternalUserLogin login) => _write.AddExternalLogin(login);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _write.GetRefreshTokenByHashForUpdateAsync(tokenHash, cancellationToken);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _write.SaveChangesAsync(cancellationToken);

    public Task InvalidateUserCacheAsync(Guid userId, string? email = null, string? userName = null, CancellationToken cancellationToken = default) =>
        _write.InvalidateUserCacheAsync(userId, email, userName, cancellationToken);

    public Task<int> RevokeAllRefreshTokensForUserAsync(Guid userId, DateTime revokedAtUtc, CancellationToken cancellationToken = default) =>
        _write.RevokeAllRefreshTokensForUserAsync(userId, revokedAtUtc, cancellationToken);
}
