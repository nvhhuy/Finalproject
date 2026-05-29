using Domains.Billing;

namespace Application.Contracts.Billing;

public interface IPlanStore
{
    Task<bool> CodeExistsAsync(string normalizedCode, CancellationToken cancellationToken = default);

    Task<Plan?> GetByCodeAsync(string normalizedCode, bool activeOnly, CancellationToken cancellationToken = default);

    Task<Plan?> GetByIdForUpdateAsync(Guid planId, CancellationToken cancellationToken = default);

    Task<bool> HasSubscriptionsAsync(Guid planId, CancellationToken cancellationToken = default);

    void Remove(Plan plan);

    void Add(Plan plan);

    void Add(Subscription subscription);

    Task<bool> TenantHasSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);
}