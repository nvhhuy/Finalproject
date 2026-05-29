using Domains.Common;
using Domains.Common.Interfaces;
using Domains.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
namespace Infrastructure.Persistence;

/// <summary>Shared EF model between write and read PostgreSQL contexts (CQRS split).</summary>
internal static class PersistenceModel
{
    private static readonly HashSet<Type> RowVersionExcludedTypes =
    [
        typeof(PersonalWallet),
        typeof(WalletLedgerEntry),
    ];

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<List<IDomainEvent>>()
            .ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ConfigureRowVersion(modelBuilder);

        DataSeed.Seed(modelBuilder);
    }

    /// <summary>
    /// Maps <see cref="Entity{TId}.RowVersion"/> as a concurrency token for domain entities.
    /// Wallet aggregates are excluded: they use ledger idempotency keys + <c>SELECT … FOR UPDATE</c> instead.
    /// </summary>
    private static void ConfigureRowVersion(ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!IsDomainEntity(entityType.ClrType) || RowVersionExcludedTypes.Contains(entityType.ClrType))
                continue;

            IMutableProperty? rowVersion = entityType.FindProperty(nameof(IHasRowVersion.RowVersion));
            if (rowVersion is null)
                continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property<uint>(nameof(IHasRowVersion.RowVersion))
                .IsRowVersion();
        }
    }

    private static bool IsDomainEntity(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Entity<>))
                return true;
        }

        return false;
    }
}
