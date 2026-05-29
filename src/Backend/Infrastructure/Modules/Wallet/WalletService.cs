using Application.Contracts.Billing;
using Application.Contracts.Persistence;
using Application.Contracts.Services;
using Application.Contracts.Transactions;
using Application.Contracts.Wallet;
using Domains.UserAggregate.ValueObjects;
using Domains.Wallet;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Wallet;

public sealed class WalletService : IWalletService
{
    private readonly IPersonalWalletStore _wallets;
    private readonly IBillingInvoiceIssuer _invoices;
    private readonly IApplicationReadDbContext _read;
    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public WalletService(
        IPersonalWalletStore wallets,
        IBillingInvoiceIssuer invoices,
        IApplicationReadDbContext read,
        ApplicationDbContext db,
        IUnitOfWork uow,
        IDateTimeProvider clock)
    {
        _wallets = wallets;
        _invoices = invoices;
        _read = read;
        _db = db;
        _uow = uow;
        _clock = clock;
    }

    public async Task<WalletBalanceDto> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        UserId userKey = UserId.Create(userId);
        PersonalWallet? wallet = await _read.PersonalWallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userKey, cancellationToken);

        return wallet is null
            ? new WalletBalanceDto(Guid.Empty, 0)
            : ToBalanceDto(wallet);
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
        WalletBalanceDto? replay = await TryGetBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        if (replay is not null)
            return replay;

        PersonalWallet wallet = await GetOrCreateWalletForUpdateAsync(userId, cancellationToken);
        wallet.TopUp(amountCredits, idempotencyKey, description ?? $"topup:{amountCredits}", _clock.Now);

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (WalletDbExceptionHelper.IsUniqueViolation(ex, WalletDbExceptionHelper.LedgerIdempotencyIndex))
        {
            _db.ChangeTracker.Clear();
            return await RequireBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        }

        await _invoices.IssueWalletTopUpReceiptAsync(
            userId,
            amountCredits,
            amountCents: 0,
            "CREDITS",
            "manual",
            cancellationToken);
        return ToBalanceDto(wallet);
    }

    public async Task<WalletBalanceDto> PurchasePackageAsync(
        Guid userId,
        Guid packageId,
        string packageCode,
        long creditsAmount,
        int priceCents,
        string currency,
        string idempotencyKey,
        string provider,
        CancellationToken cancellationToken = default)
    {
        WalletBalanceDto? replay = await TryGetBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        if (replay is not null)
            return replay;

        PersonalWallet wallet = await GetOrCreateWalletForUpdateAsync(userId, cancellationToken);
        string description = $"package:{packageCode}";
        wallet.TopUp(creditsAmount, idempotencyKey, description, _clock.Now);

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (WalletDbExceptionHelper.IsUniqueViolation(ex, WalletDbExceptionHelper.LedgerIdempotencyIndex))
        {
            _db.ChangeTracker.Clear();
            return await RequireBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        }

        await _invoices.IssueWalletTopUpReceiptAsync(
            userId,
            creditsAmount,
            priceCents,
            currency,
            provider,
            cancellationToken);
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

        WalletBalanceDto? replayBalance = await TryGetBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        if (replayBalance is not null)
        {
            WalletLedgerEntryDto? replayEntry = await FindLedgerEntryAsync(replayBalance.WalletId, idempotencyKey, cancellationToken);
            if (replayEntry is not null)
            {
                return new WalletDebitResultDto(
                    replayEntry.AmountCredits < 0,
                    Math.Abs(replayEntry.AmountCredits),
                    replayBalance.BalanceCredits);
            }
        }

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
        catch (DbUpdateException ex) when (WalletDbExceptionHelper.IsUniqueViolation(ex, WalletDbExceptionHelper.LedgerIdempotencyIndex))
        {
            _db.ChangeTracker.Clear();
            WalletBalanceDto balance = await RequireBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
            WalletLedgerEntryDto entry = await FindLedgerEntryAsync(balance.WalletId, idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("Idempotent debit replay entry was not found.");
            return new WalletDebitResultDto(
                entry.AmountCredits < 0,
                Math.Abs(entry.AmountCredits),
                balance.BalanceCredits);
        }
    }

    private async Task<WalletBalanceDto?> TryGetBalanceForIdempotencyReplayAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return null;

        UserId userKey = UserId.Create(userId);
        var row = await (
            from entry in _read.WalletLedgerEntries.AsNoTracking()
            join wallet in _read.PersonalWallets.AsNoTracking() on entry.WalletId equals wallet.Id
            where entry.IdempotencyKey == idempotencyKey && wallet.UserId == userKey
            select new { wallet.Id, entry.BalanceAfterCredits })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new WalletBalanceDto(row.Id, row.BalanceAfterCredits);
    }

    private async Task<WalletBalanceDto> RequireBalanceForIdempotencyReplayAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        WalletBalanceDto? replay = await TryGetBalanceForIdempotencyReplayAsync(userId, idempotencyKey, cancellationToken);
        return replay ?? throw new InvalidOperationException("Idempotent wallet mutation replay could not be resolved.");
    }

    private async Task<WalletLedgerEntryDto?> FindLedgerEntryAsync(
        Guid walletId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await _read.WalletLedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == walletId && e.IdempotencyKey == idempotencyKey)
            .Select(e => new WalletLedgerEntryDto(
                e.Id,
                e.EntryType.ToString(),
                e.AmountCredits,
                e.BalanceAfterCredits,
                e.ActionCode,
                e.Description,
                e.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PersonalWallet> GetOrCreateWalletForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserId userKey = UserId.Create(userId);
        PersonalWallet? wallet = await _wallets.GetByUserIdForUpdateAsync(userKey, cancellationToken);
        if (wallet is not null)
            return wallet;

        wallet = PersonalWallet.CreateForUser(userKey, _clock.Now);
        _wallets.Add(wallet);

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (WalletDbExceptionHelper.IsUniqueViolation(ex, WalletDbExceptionHelper.WalletUserUniqueIndex))
        {
            _db.ChangeTracker.Clear();
            wallet = await _wallets.GetByUserIdForUpdateAsync(userKey, cancellationToken);
            if (wallet is null)
                throw new InvalidOperationException("Failed to acquire wallet row after concurrent create.");
            return wallet;
        }

        return wallet;
    }

    private static WalletBalanceDto ToBalanceDto(PersonalWallet wallet) =>
        new(wallet.Id, wallet.BalanceCredits);
}
