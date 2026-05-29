using Domains.Common;

namespace Domains.Wallet;

public enum WalletLedgerEntryType
{
    TopUp = 0,
    Debit = 1,
    Refund = 2,
    Hold = 3,
    Release = 4,
}

public sealed class WalletLedgerEntry : Entity<Guid>
{
    public Guid WalletId { get; private set; }
    public WalletLedgerEntryType EntryType { get; private set; }
    public long AmountCredits { get; private set; }
    public long BalanceAfterCredits { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? ActionCode { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private WalletLedgerEntry() { }

    private WalletLedgerEntry(
        Guid id,
        Guid walletId,
        WalletLedgerEntryType entryType,
        long amountCredits,
        long balanceAfterCredits,
        string? idempotencyKey,
        string? actionCode,
        string? description,
        DateTime createdAtUtc) : base(id)
    {
        WalletId = walletId;
        EntryType = entryType;
        AmountCredits = amountCredits;
        BalanceAfterCredits = balanceAfterCredits;
        IdempotencyKey = idempotencyKey;
        ActionCode = actionCode;
        Description = description;
        CreatedAtUtc = createdAtUtc;
    }

    public static WalletLedgerEntry Create(
        Guid walletId,
        WalletLedgerEntryType entryType,
        long amountCredits,
        long balanceAfterCredits,
        string? idempotencyKey,
        string? actionCode,
        string? description,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            walletId,
            entryType,
            amountCredits,
            balanceAfterCredits,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            string.IsNullOrWhiteSpace(actionCode) ? null : actionCode.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            createdAtUtc);
}
