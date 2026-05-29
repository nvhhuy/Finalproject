using Domains.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Support.Persistence;

internal sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TicketNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedSubject).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CreatedByUserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.Status, x.Priority, x.UpdatedAtUtc });
        builder.HasIndex(x => new { x.AssignedAdminUserId, x.Status, x.UpdatedAtUtc });
        builder.HasIndex(x => x.NormalizedSubject);

        builder.HasMany(x => x.Messages)
            .WithOne()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
