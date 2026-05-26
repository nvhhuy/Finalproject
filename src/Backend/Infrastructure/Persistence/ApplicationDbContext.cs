using Domains.Administration;
using Domains.MediaAggregate;
using Domains.UserAggregate;
using Domains.SystemSettings;
using Infrastructure.Messaging.Outbox;
using Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// CQRS write-side DbContext. Tracking enabled. Use <c>IUnitOfWork.SaveChangesAsync</c> from
/// command handlers — do not call <c>SaveChanges</c> directly on this context.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    private readonly DomainEventsSaveChangesInterceptor _domainEventsInterceptor;

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ExternalUserLogin> ExternalLogins => Set<ExternalUserLogin>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        DomainEventsSaveChangesInterceptor domainEventsInterceptor) : base(options)
    {
        _domainEventsInterceptor = domainEventsInterceptor;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_domainEventsInterceptor);
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        PersistenceModel.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
