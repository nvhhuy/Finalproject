using Domains.Common.Interfaces;
using Domains.UserAggregate;
using Infrastructure.Persistence.Configuration;
using Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
     
        private readonly AuditableInterceptor _auditableInterceptor;

        public DbSet<User> Users { get; set; } = null!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, AuditableInterceptor auditableInterceptor) : base(options)
        {
            _auditableInterceptor = auditableInterceptor;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(_auditableInterceptor);
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Ignore<List<IDomainEvent>>()
                .ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            AuditableConfiguration.Configure(modelBuilder);

            DataSeed.Seed(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
