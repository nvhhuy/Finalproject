using Domains.MediaAggregate;
using Domains.MediaAggregate.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Media.Persistence.Configuration;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion(id => id.Value, value => MediaAssetId.Create(value));

        builder.Property(e => e.OwnerUserId).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>().IsRequired();
        builder.Property(e => e.Status).HasConversion<int>().IsRequired();
        builder.Property(e => e.OriginalKey).HasMaxLength(512).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ByteSize).IsRequired();
        builder.Property(e => e.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(e => e.UploadedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.ProcessingStartedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.ProcessingAttemptCount).IsRequired();
        builder.Property(e => e.ProcessedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        // Variants are an owned collection on the aggregate root: enforced cascade delete and
        // they cannot be reached from any other repository. We use a backing field for the list
        // so reads stay sealed behind MediaAsset.Variants.
        builder.OwnsMany(e => e.Variants, variant =>
        {
            variant.ToTable("MediaVariants");
            variant.WithOwner().HasForeignKey("MediaAssetId");
            variant.HasKey(v => v.Id);
            variant.Property(v => v.Kind).HasConversion<int>().IsRequired();
            variant.Property(v => v.StorageKey).HasMaxLength(512).IsRequired();
            variant.Property(v => v.ContentType).HasMaxLength(128).IsRequired();
            variant.Property(v => v.ByteSize).IsRequired();
            variant.Property(v => v.Width);
            variant.Property(v => v.Height);
            variant.Property(v => v.Duration);
            variant.HasIndex("MediaAssetId", nameof(MediaVariant.Kind));
        });

        builder.Navigation(e => e.Variants).HasField("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.OwnerUserId);
        builder.HasIndex(e => e.Status);

        // Composite (OwnerUserId, Status) index supports the per-user-asset-by-status feeds
        // (e.g. "my Ready uploads" or "my Failed uploads") which would otherwise scan every
        // row a user has ever uploaded. Order matters: OwnerUserId first because every query
        // filters by it, Status second to narrow within a user.
        builder.HasIndex(e => new { e.OwnerUserId, e.Status });

        // Composite index supports the stale-recovery query (Status=Processing AND
        // ProcessingStartedAtUtc < cutoff). Without it, the scan would touch every row in the
        // table at every cron tick.
        builder.HasIndex(e => new { e.Status, e.ProcessingStartedAtUtc });
    }
}
