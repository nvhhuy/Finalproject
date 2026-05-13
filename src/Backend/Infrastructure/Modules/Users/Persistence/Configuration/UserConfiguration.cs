using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Users.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => UserId.Create(value));
        builder.Property(e => e.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Password).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LockReason).HasMaxLength(512);
        builder.Property(e => e.TwoFactorSecret).HasMaxLength(512);
        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.UserName).IsUnique();
    }
}
