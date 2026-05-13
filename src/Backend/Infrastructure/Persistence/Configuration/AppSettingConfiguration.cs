using Domains.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configuration;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.Value).HasMaxLength(2048);
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
