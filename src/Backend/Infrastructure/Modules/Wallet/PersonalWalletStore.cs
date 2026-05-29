using Application.Contracts.Wallet;
using Domains.UserAggregate.ValueObjects;
using Domains.Wallet;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Wallet;

public sealed class PersonalWalletStore : IPersonalWalletStore
{
    private readonly ApplicationDbContext _db;

    public PersonalWalletStore(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Pessimistic row lock when the wallet exists — must run inside an active DB transaction.
    /// Returns null when no wallet row exists yet (caller may insert under the same transaction).
    /// </summary>
    public async Task<PersonalWallet?> GetByUserIdForUpdateAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = userId.Value;

        PersonalWallet? wallet = await _db.PersonalWallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        if (wallet is null)
            return null;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT "Id"
             FROM "PersonalWallets"
             WHERE "UserId" = {userGuid}
             FOR UPDATE
             """,
            cancellationToken);

        return await _db.PersonalWallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
    }

    public void Add(PersonalWallet wallet) => _db.PersonalWallets.Add(wallet);
}
