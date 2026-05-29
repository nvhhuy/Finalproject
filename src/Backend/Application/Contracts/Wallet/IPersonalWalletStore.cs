using Domains.UserAggregate.ValueObjects;
using Domains.Wallet;

namespace Application.Contracts.Wallet;

public interface IPersonalWalletStore
{
    Task<PersonalWallet?> GetByUserIdForUpdateAsync(UserId userId, CancellationToken cancellationToken = default);

    void Add(PersonalWallet wallet);
}