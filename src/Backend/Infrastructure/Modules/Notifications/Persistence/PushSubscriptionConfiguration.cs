using Domains.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Notifications.Persistence;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Endpoint).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.HasIndex(x => new { x.UserId, x.Endpoint }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
