using Domains.UserAggregate;
using Domains.SystemSettings;
using Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
     
        private readonly AuditableInterceptor _auditableInterceptor;
        private readonly DomainEventsSaveChangesInterceptor _domainEventsInterceptor;

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<ExternalUserLogin> ExternalLogins { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            AuditableInterceptor auditableInterceptor,
            DomainEventsSaveChangesInterceptor domainEventsInterceptor) : base(options)
        {
            _auditableInterceptor = auditableInterceptor;
            _domainEventsInterceptor = domainEventsInterceptor;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_auditableInterceptor, _domainEventsInterceptor);
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            PersistenceModel.Configure(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
