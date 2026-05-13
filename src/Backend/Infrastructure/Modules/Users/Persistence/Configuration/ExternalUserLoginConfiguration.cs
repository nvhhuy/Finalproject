using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Users.Persistence.Configuration;

public sealed class ExternalUserLoginConfiguration : IEntityTypeConfiguration<ExternalUserLogin>
{
    public void Configure(EntityTypeBuilder<ExternalUserLogin> builder)
    {
        builder.ToTable("ExternalUserLogins");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).HasConversion(id => id.Value, v => UserId.Create(v));
        builder.Property(e => e.Provider).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderSubject).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => new { e.Provider, e.ProviderSubject }).IsUnique();
        builder.HasOne<User>().WithMany(u => u.ExternalLogins).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
