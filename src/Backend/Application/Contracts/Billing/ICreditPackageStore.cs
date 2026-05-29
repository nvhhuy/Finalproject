using Domains.Billing;

namespace Application.Contracts.Billing;

public interface ICreditPackageStore
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default);

    Task<CreditPackage?> GetByCodeAsync(string normalizedCode, bool activeOnly, CancellationToken cancellationToken = default);

    Task<CreditPackage?> GetByIdForUpdateAsync(Guid packageId, CancellationToken cancellationToken = default);

    void Add(CreditPackage package);

    void Remove(CreditPackage package);
}
