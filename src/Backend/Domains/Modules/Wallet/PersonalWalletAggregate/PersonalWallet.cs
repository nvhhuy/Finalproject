using Domains.Common;
using Domains.UserAggregate.ValueObjects;

namespace Domains.Wallet;

public sealed class PersonalWallet : AggregateRoot<Guid>
{
    private readonly List<WalletLedgerEntry> _entries = [];

    private PersonalWallet()
    {
    }

    private PersonalWallet(Guid id, UserId userId, DateTime utcNow) : base(id)
    {
        UserId = userId;
        BalanceCredits = 0;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public UserId UserId { get; private set; } = null!;
    public long BalanceCredits { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyList<WalletLedgerEntry> Entries => _entries.AsReadOnly();

    public static PersonalWallet CreateForUser(UserId userId, DateTime utcNow)
    {
        return new PersonalWallet(Guid.NewGuid(), userId, utcNow);
    }

    public WalletLedgerEntry TopUp(long amountCredits, string? idempotencyKey, string? description, DateTime utcNow)
    {
        if (amountCredits <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCredits), "Top-up amount must be positive.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            _entries.Any(e => e.IdempotencyKey == idempotencyKey))
            return _entries.First(e => e.IdempotencyKey == idempotencyKey);

        BalanceCredits = checked(BalanceCredits + amountCredits);
        UpdatedAtUtc = utcNow;
        var entry = WalletLedgerEntry.Create(
            Id,
            WalletLedgerEntryType.TopUp,
            amountCredits,
            BalanceCredits,
            idempotencyKey,
            null,
            description,
            utcNow);
        _entries.Add(entry);
        return entry;
    }

    public WalletLedgerEntry Debit(
        long amountCredits,
        string actionCode,
        string idempotencyKey,
        string? description,
        DateTime utcNow)
    {
        if (amountCredits <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountCredits), "Debit amount must be positive.");
        if (string.IsNullOrWhiteSpace(actionCode))
            throw new ArgumentException("Action code is required.", nameof(actionCode));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));

        var existing = _entries.FirstOrDefault(e => e.IdempotencyKey == idempotencyKey);
        if (existing is not null)
            return existing;

        if (BalanceCredits < amountCredits)
            throw new InvalidOperationException("Insufficient wallet balance.");

        BalanceCredits = checked(BalanceCredits - amountCredits);
        UpdatedAtUtc = utcNow;
        var entry = WalletLedgerEntry.Create(
            Id,
            WalletLedgerEntryType.Debit,
            -amountCredits,
            BalanceCredits,
            idempotencyKey,
            actionCode,
            description,
            utcNow);
        _entries.Add(entry);
        return entry;
    }
}