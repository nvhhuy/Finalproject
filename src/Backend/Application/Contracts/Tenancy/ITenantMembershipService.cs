namespace Application.Contracts.Tenancy;

public interface ITenantMembershipService
{
    Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTenantIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken cancellationToken = default);
}