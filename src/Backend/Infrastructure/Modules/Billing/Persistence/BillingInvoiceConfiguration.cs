using Domains.Billing;
using Domains.Tenancy.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Billing.Persistence;

public sealed class BillingInvoiceConfiguration : IEntityTypeConfiguration<BillingInvoice>
{
    public void Configure(EntityTypeBuilder<BillingInvoice> builder)
    {
        builder.ToTable("BillingInvoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.TenantId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value == null ? null : TenantId.Create(value.Value));
        builder.Property(i => i.InvoiceNumber).HasMaxLength(32).IsRequired();
        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.Property(i => i.Description).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Currency).HasMaxLength(8).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.Provider).HasMaxLength(32).IsRequired();
        builder.Property(i => i.ExternalReference).HasMaxLength(128);
        builder.Property(i => i.IssuedAtUtc).IsRequired();
        builder.HasIndex(i => new { i.TenantId, i.IssuedAtUtc });
        builder.HasIndex(i => new { i.UserId, i.IssuedAtUtc });
    }
}