using Domains.Contact;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public sealed class ContactMessageConfiguration : IEntityTypeConfiguration<ContactMessage>
{
    public void Configure(EntityTypeBuilder<ContactMessage> builder)
    {
        builder.ToTable("ContactMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Email).HasMaxLength(256).IsRequired();
        builder.Property(m => m.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Subject).HasMaxLength(256).IsRequired();
        builder.Property(m => m.Body).HasMaxLength(4000).IsRequired();
        builder.HasIndex(m => m.SubmittedAtUtc);
        builder.HasIndex(m => m.NormalizedEmail);
        builder.HasIndex(m => m.IsRead);
    }
}
