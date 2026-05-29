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

    public Task<PersonalWallet?> GetByUserIdForUpdateAsync(UserId userId, CancellationToken cancellationToken = default) =>
        _db.PersonalWallets
            .Include(w => w.Entries)
            .FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);

    public void Add(PersonalWallet wallet) => _db.PersonalWallets.Add(wallet);
}
