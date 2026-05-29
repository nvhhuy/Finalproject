using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Tenancy.Persistence;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => TenantId.Create(value));

        builder.Property(t => t.Slug).HasMaxLength(63).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(t => t.OwnerUserId)
            .HasConversion(id => id.Value, value => UserId.Create(value))
            .IsRequired();
        builder.Property(t => t.PublicTagline).HasMaxLength(300);
        builder.Property(t => t.PublicDescription).HasMaxLength(4000);
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        builder.HasMany(t => t.Members)
            .WithOne()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Members).HasField("_members");
    }
}