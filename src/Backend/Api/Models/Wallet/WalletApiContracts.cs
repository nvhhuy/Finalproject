using Application.Contracts.Wallet;

namespace Api.Models.Wallet;

public sealed record WalletBalanceResponse(Guid WalletId, long BalanceCredits);

public sealed record WalletLedgerEntryResponse(
    Guid Id,
    string EntryType,
    long AmountCredits,
    long BalanceAfterCredits,
    string? ActionCode,
    string? Description,
    DateTime CreatedAtUtc);

public sealed record TopUpWalletRequest(long AmountCredits);

public sealed record DebitWalletActionRequest(string ActionCode);

public sealed record WalletDebitResponse(
    bool Success,
    long ChargedCredits,
    long RemainingBalance,
    string? DenyReason);

public sealed record MeteredActionResponse(string Code, long CostCredits, string Description);

public static class WalletApiMapping
{
    public static WalletBalanceResponse ToResponse(WalletBalanceDto dto) =>
        new(dto.WalletId, dto.BalanceCredits);

    public static WalletLedgerEntryResponse ToResponse(WalletLedgerEntryDto dto) =>
        new(dto.Id, dto.EntryType, dto.AmountCredits, dto.BalanceAfterCredits, dto.ActionCode, dto.Description, dto.CreatedAtUtc);

    public static WalletDebitResponse ToResponse(WalletDebitResultDto dto) =>
        new(dto.Success, dto.ChargedCredits, dto.RemainingBalance, dto.DenyReason);

    public static MeteredActionResponse ToResponse(MeteredActionDto dto) =>
        new(dto.Code, dto.CostCredits, dto.Description);
}
