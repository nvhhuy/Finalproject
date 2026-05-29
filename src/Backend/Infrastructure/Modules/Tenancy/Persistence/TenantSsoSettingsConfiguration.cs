using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Tenancy.Persistence;

public sealed class TenantSsoSettingsConfiguration : IEntityTypeConfiguration<TenantSsoSettings>
{
    public void Configure(EntityTypeBuilder<TenantSsoSettings> builder)
    {
        builder.ToTable("TenantSsoSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .IsRequired();
        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.Property(s => s.IsEnabled).IsRequired();
        builder.Property(s => s.OidcAuthority).HasMaxLength(500).IsRequired();
        builder.Property(s => s.OidcClientId).HasMaxLength(200).IsRequired();
        builder.Property(s => s.OidcClientSecret).HasMaxLength(500);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
    }
}