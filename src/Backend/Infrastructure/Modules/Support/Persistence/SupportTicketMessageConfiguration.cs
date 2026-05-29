using Domains.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Support.Persistence;

internal sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.ToTable("SupportTicketMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.TicketId, x.CreatedAtUtc });

        builder.HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.TicketMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}