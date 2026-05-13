using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Users.Persistence.Configuration;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).HasConversion(id => id.Value, v => UserId.Create(v));
        builder.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ReplacedByTokenHash).HasMaxLength(128);
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany(u => u.RefreshTokens).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
