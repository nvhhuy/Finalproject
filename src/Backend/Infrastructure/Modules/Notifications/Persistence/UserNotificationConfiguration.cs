using Domains.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Notifications.Persistence;

internal sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.LinkUrl).HasMaxLength(512);
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.UserId, x.ReadAtUtc });
        builder.Property(x => x.LiveDeliveredAtUtc);
        builder.HasIndex(x => new { x.LiveDeliveredAtUtc, x.CreatedAtUtc });
    }
}