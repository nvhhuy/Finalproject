using Domains.UserAggregate.ValueObjects;
using Domains.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Modules.Wallet.Persistence;

public sealed class PersonalWalletConfiguration : IEntityTypeConfiguration<PersonalWallet>
{
    public void Configure(EntityTypeBuilder<PersonalWallet> builder)
    {
        builder.ToTable("PersonalWallets");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.UserId)
            .HasConversion(id => id.Value, value => UserId.Create(value))
            .IsRequired();
        builder.HasIndex(w => w.UserId).IsUnique();
        builder.Property(w => w.BalanceCredits).IsRequired();
        builder.Property(w => w.CreatedAtUtc).IsRequired();
        builder.Property(w => w.UpdatedAtUtc).IsRequired();

        builder.HasMany(w => w.Entries)
            .WithOne()
            .HasForeignKey(e => e.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(w => w.Entries).HasField("_entries");
    }
}

public sealed class WalletLedgerEntryConfiguration : IEntityTypeConfiguration<WalletLedgerEntry>
{
    public void Configure(EntityTypeBuilder<WalletLedgerEntry> builder)
    {
        builder.ToTable("WalletLedgerEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.AmountCredits).IsRequired();
        builder.Property(e => e.BalanceAfterCredits).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(128);
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.Property(e => e.ActionCode).HasMaxLength(64);
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => new { e.WalletId, e.CreatedAtUtc });
    }
}
