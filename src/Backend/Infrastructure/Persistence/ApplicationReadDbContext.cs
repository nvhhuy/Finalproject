using Application.Contracts.Modules.Users.Persistence;
using Domains.SystemSettings;
using Domains.UserAggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class ApplicationReadDbContext : DbContext, IApplicationReadDbContext
{
    public ApplicationReadDbContext(DbContextOptions<ApplicationReadDbContext> options) : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalUserLogin> ExternalLogins => Set<ExternalUserLogin>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PersistenceModel.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
