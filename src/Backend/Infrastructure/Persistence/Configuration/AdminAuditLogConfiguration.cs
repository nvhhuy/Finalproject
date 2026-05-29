using Domains.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.Details).HasMaxLength(1024);
        builder.HasIndex(e => e.CreatedAtUtc);
        builder.HasIndex(e => e.ActorUserId);
        builder.HasIndex(e => e.TargetUserId);
    }
}