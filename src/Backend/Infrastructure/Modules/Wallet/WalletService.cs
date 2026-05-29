using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Contracts.Wallet;
using Domains.UserAggregate.ValueObjects;
using Domains.Wallet;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Wallet;

public sealed class WalletService : IWalletService
{
    private readonly IPersonalWalletStore _wallets;
    private readonly IBillingInvoiceIssuer _invoices;
    private readonly IApplicationReadDbContext _read;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public WalletService(
        IPersonalWalletStore wallets,
        IBillingInvoiceIssuer invoices,
        IApplicationReadDbContext read,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _wallets = wallets;
        _invoices = invoices;
        _read = read;
        _uow = uow;
        _clock = clock;
    }

    public async Task<WalletBalanceDto> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        PersonalWallet wallet = await GetOrCreateWalletAsync(userId, cancellationToken);
        return ToBalanceDto(wallet);
    }

    public async Task<IReadOnlyList<WalletLedgerEntryDto>> GetLedgerAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        UserId userKey = UserId.Create(userId);
        Guid? walletId = await _read.PersonalWallets.AsNoTracking()
            .Where(w => w.UserId == userKey)
            .Select(w => w.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (walletId is null || walletId == Guid.Empty)
            return Array.Empty<WalletLedgerEntryDto>();

        int skip = Math.Max(0, page - 1) * Math.Clamp(pageSize, 1, 100);
        return await _read.WalletLedgerEntries
            .AsNoTracking()
            .Where(e => e.WalletId == walletId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip(skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(e => new WalletLedgerEntryDto(
                e.Id,
                e.EntryType.ToString(),
                e.AmountCredits,
                e.BalanceAfterCredits,
                e.ActionCode,
                e.Description,
                e.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<WalletBalanceDto> TopUpAsync(
        Guid userId,
        long amountCredits,
        string idempotencyKey,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        PersonalWallet wallet = await GetOrCreateWalletForUpdateAsync(userId, cancellationToken);
        wallet.TopUp(amountCredits, idempotencyKey, description ?? $"topup:{amountCredits}", _clock.Now);
        await _uow.SaveChangesAsync(cancellationToken);
        await _invoices.IssueWalletTopUpReceiptAsync(userId, amountCredits, "manual", cancellationToken);
        return ToBalanceDto(wallet);
    }

    public async Task<WalletDebitResultDto> DebitForActionAsync(
        Guid userId,
        string actionCode,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!MeteredActions.TryGetCost(actionCode, out long cost))
            return new WalletDebitResultDto(false, 0, 0, "Unknown metered action.");

        PersonalWallet wallet = await GetOrCreateWalletForUpdateAsync(userId, cancellationToken);
        try
        {
            wallet.Debit(cost, actionCode, idempotencyKey, actionCode, _clock.Now);
            await _uow.SaveChangesAsync(cancellationToken);
            return new WalletDebitResultDto(true, cost, wallet.BalanceCredits);
        }
        catch (InvalidOperationException)
        {
            return new WalletDebitResultDto(false, cost, wallet.BalanceCredits, "Insufficient wallet balance.");
        }
    }

    private async Task<PersonalWallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserId userKey = UserId.Create(userId);
        PersonalWallet? wallet = await _read.PersonalWallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userKey, cancellationToken);
        if (wallet is not null)
            return wallet;

        wallet = PersonalWallet.CreateForUser(userKey, _clock.Now);
        _wallets.Add(wallet);
        await _uow.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private async Task<PersonalWallet> GetOrCreateWalletForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserId userKey = UserId.Create(userId);
        PersonalWallet? wallet = await _wallets.GetByUserIdForUpdateAsync(userKey, cancellationToken);
        if (wallet is not null)
            return wallet;

        wallet = PersonalWallet.CreateForUser(userKey, _clock.Now);
        _wallets.Add(wallet);
        await _uow.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static WalletBalanceDto ToBalanceDto(PersonalWallet wallet) =>
        new(wallet.Id, wallet.BalanceCredits);
}
