using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Modules.Users.Persistence.Conversions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Users.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>Purpose string for <see cref="IDataProtector"/> — changing it breaks decryption of stored secrets.</summary>
    public const string TwoFactorSecretProtectorPurpose = "User.TwoFactorSecret.v1";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => UserId.Create(value));
        builder.Property(e => e.FirstName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.UserName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PasswordHash).HasColumnName("Password").HasMaxLength(512).IsRequired();
        builder.Property(e => e.Role).HasMaxLength(64).IsRequired();
        builder.Property(e => e.LockReason).HasMaxLength(512);

        IDataProtector twoFactorProtector = UserDataProtection.Provider.CreateProtector(TwoFactorSecretProtectorPurpose);
        builder.Property(e => e.TwoFactorSecret)
            .HasMaxLength(1024)
            .HasConversion(new EncryptedStringConverter(twoFactorProtector));

        builder.Property(e => e.EmailVerifiedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.RegisteredAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(e => e.Email).IsUnique();
        builder.HasIndex(e => e.UserName).IsUnique();
    }
}
