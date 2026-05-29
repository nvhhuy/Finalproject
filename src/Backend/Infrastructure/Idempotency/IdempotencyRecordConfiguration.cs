using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Idempotency;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasMaxLength(256).IsRequired();
        builder.Property(x => x.OwnerPartition).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.ResponseBody).HasColumnType("text");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(x => new { x.Scope, x.OwnerPartition, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}