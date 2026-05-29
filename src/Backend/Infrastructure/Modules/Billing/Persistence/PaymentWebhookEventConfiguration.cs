using Domains.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Billing.Persistence;

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ExternalEventId).HasMaxLength(128).IsRequired();
        builder.HasIndex(e => new { e.Provider, e.ExternalEventId }).IsUnique();
        builder.Property(e => e.EventType).HasMaxLength(128).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.ReceivedAtUtc).IsRequired();
        builder.Property(e => e.ProcessedAtUtc);
        builder.HasIndex(e => e.ReceivedAtUtc);
    }
}