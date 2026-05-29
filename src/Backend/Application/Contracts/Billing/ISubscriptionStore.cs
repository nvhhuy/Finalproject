using Domains.Billing;
using Domains.Tenancy.ValueObjects;

namespace Application.Contracts.Billing;

public interface ISubscriptionStore
{
    Task<Subscription?> GetByTenantForUpdateAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> ListExpiredActiveOrTrialingAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> ListPastDueAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Subscription>> ListActiveOrTrialingForUpdateAsync(CancellationToken cancellationToken = default);
}
