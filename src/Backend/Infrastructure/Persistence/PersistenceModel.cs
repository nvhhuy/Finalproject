using Domains.Common.Interfaces;
using Infrastructure.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>Shared EF model between write and read PostgreSQL contexts (CQRS split).</summary>
internal static class PersistenceModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<List<IDomainEvent>>()
            .ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        AuditableConfiguration.Configure(modelBuilder);

        DataSeed.Seed(modelBuilder);
    }
}
