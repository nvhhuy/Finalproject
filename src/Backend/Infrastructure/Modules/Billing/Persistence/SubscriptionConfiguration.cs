using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Billing.Persistence;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TenantId)
            .HasConversion(id => id.Value, value => TenantId.Create(value))
            .IsRequired();
        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.Property(s => s.PlanId).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.CurrentPeriodStartUtc).IsRequired();
        builder.Property(s => s.CurrentPeriodEndUtc).IsRequired();
        builder.Property(s => s.CanceledAtUtc);
        builder.Property(s => s.LastMonthlyCreditGrantUtc);
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.HasIndex(s => new { s.Status, s.CurrentPeriodEndUtc });
    }
}