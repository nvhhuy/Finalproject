using Domains.Wallet;
using Domains.UserAggregate.ValueObjects;

namespace Application.Contracts.Wallet;

public interface IPersonalWalletStore
{
    Task<PersonalWallet?> GetByUserIdForUpdateAsync(UserId userId, CancellationToken cancellationToken = default);

    void Add(PersonalWallet wallet);
}
