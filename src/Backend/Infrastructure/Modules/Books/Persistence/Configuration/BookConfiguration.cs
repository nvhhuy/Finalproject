using Domains.Modules.Books.BookAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Books.Persistence.Configuration;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => BookId.Create(value));

        builder.Property(e => e.OwnerUserId).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Author).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Isbn).HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(e => e.OwnerUserId);
        builder.HasIndex(e => new { e.OwnerUserId, e.UpdatedAtUtc });
    }
}
