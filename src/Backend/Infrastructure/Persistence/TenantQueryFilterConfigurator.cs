using Application.Contracts.Tenancy;
using Domains.Common;
using Domains.MediaAggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal static class TenantQueryFilterConfigurator
{
    public static void Apply(ModelBuilder modelBuilder, ITenantContext tenantContext)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
                continue;

            ConfigureFilter(modelBuilder, entityType.ClrType, tenantContext);
        }
    }

    private static void ConfigureFilter(ModelBuilder modelBuilder, Type clrType, ITenantContext tenantContext)
    {
        if (clrType == typeof(MediaAsset))
            modelBuilder.Entity<MediaAsset>()
                .HasQueryFilter(e =>
                    tenantContext.BypassTenantIsolation
                    || (tenantContext.TenantId != null && e.TenantId == tenantContext.TenantId));
    }
}