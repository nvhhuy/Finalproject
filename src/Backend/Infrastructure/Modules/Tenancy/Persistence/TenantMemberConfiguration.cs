using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Tenancy.Persistence;

public sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        builder.ToTable("TenantMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .IsRequired();
        builder.Property(m => m.UserId)
            .HasConversion(id => id.Value, value => UserId.Create(value))
            .IsRequired();
        builder.Property(m => m.Role).HasMaxLength(32).IsRequired();
        builder.Property(m => m.JoinedAtUtc).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);
    }
}