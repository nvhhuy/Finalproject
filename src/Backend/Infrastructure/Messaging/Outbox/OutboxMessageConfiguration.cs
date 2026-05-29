using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Messaging.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventId).IsRequired();
        builder.Property(e => e.OccurredOnUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(e => e.Type).HasMaxLength(512).IsRequired();
        builder.Property(e => e.RoutingKey).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Exchange).HasMaxLength(256);
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.ProcessedOnUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.AbandonedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.Attempts).IsRequired();
        builder.Property(e => e.NextAttemptUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.Error).HasColumnType("text");
        builder.Property(e => e.CorrelationId).HasMaxLength(64);

        builder.HasIndex(e => new { e.ProcessedOnUtc, e.AbandonedAtUtc, e.NextAttemptUtc, e.OccurredOnUtc })
            .HasDatabaseName("IX_OutboxMessages_Pending");
        builder.HasIndex(e => e.EventId).IsUnique();
    }
}