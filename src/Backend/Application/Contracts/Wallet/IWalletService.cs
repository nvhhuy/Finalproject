namespace Application.Contracts.Wallet;

public interface IWalletService
{
    Task<WalletBalanceDto> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WalletLedgerEntryDto>> GetLedgerAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<WalletBalanceDto> TopUpAsync(
        Guid userId,
        long amountCredits,
        string idempotencyKey,
        string? description = null,
        CancellationToken cancellationToken = default);

    Task<WalletDebitResultDto> DebitForActionAsync(
        Guid userId,
        string actionCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed record WalletBalanceDto(Guid WalletId, long BalanceCredits);

public sealed record WalletLedgerEntryDto(
    Guid Id,
    string EntryType,
    long AmountCredits,
    long BalanceAfterCredits,
    string? ActionCode,
    string? Description,
    DateTime CreatedAtUtc);

public sealed record WalletDebitResultDto(
    bool Success,
    long ChargedCredits,
    long RemainingBalance,
    string? DenyReason = null);
