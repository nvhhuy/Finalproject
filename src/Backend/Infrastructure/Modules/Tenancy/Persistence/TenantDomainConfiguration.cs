using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Tenancy.Persistence;

public sealed class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>
{
    public void Configure(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("TenantDomains");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .IsRequired();
        builder.Property(d => d.Hostname).HasMaxLength(253).IsRequired();
        builder.HasIndex(d => d.Hostname).IsUnique();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(d => d.VerificationToken).HasMaxLength(64).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();
        builder.Property(d => d.VerifiedAtUtc);
    }
}
