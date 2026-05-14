using Application.Contracts.Modules.Users.Persistence;
using Application.Contracts.Caching;
using Application.Contracts.Modules.Users.Persistence.Write;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Users.Persistence.Repositories;

public sealed class UserWriteRepository : WriteRepositoryBase, IUserWriteRepository
{
    private readonly ApplicationDbContext _context;

    public UserWriteRepository(ApplicationDbContext context, ICacheService cache) : base(cache)
    {
        _context = context;
    }

    public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetTrackedForUpdateAsync(_context.Users, u => u.Id.Value == id, cancellationToken);

    public void AddUser(User user) => AddEntity(_context.Users, user);

    public void AddExternalLogin(ExternalUserLogin login) => AddEntity(_context.ExternalLogins, login);

    public Task<RefreshToken?> GetRefreshTokenByHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        GetTrackedForUpdateAsync(_context.RefreshTokens, t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<int> RevokeAllRefreshTokensForUserAsync(Guid userId, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        UserId uid = UserId.Create(userId);
        return await _context.RefreshTokens
            .Where(t => t.UserId == uid && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, revokedAtUtc), cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken) > 0;

    public Task InvalidateUserCacheAsync(Guid userId, string? email = null, string? userName = null, CancellationToken cancellationToken = default) =>
        InvalidateUserLookupAsync(userId, email, userName, cancellationToken);
}
