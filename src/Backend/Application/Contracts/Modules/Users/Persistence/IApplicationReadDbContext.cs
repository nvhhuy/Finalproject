using Domains.UserAggregate;
using Microsoft.EntityFrameworkCore;

namespace Application.Contracts.Modules.Users.Persistence;

/// <summary>Read-only PostgreSQL surface for queries (CQRS read side).</summary>
public interface IApplicationReadDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ExternalUserLogin> ExternalLogins { get; }
}
