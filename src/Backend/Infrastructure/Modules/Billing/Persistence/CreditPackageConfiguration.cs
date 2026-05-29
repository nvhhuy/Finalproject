using Domains.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Billing.Persistence;

public sealed class CreditPackageConfiguration : IEntityTypeConfiguration<CreditPackage>
{
    public void Configure(EntityTypeBuilder<CreditPackage> builder)
    {
        builder.ToTable("CreditPackages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.CreditsAmount).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.SortOrder).IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
    }
}
