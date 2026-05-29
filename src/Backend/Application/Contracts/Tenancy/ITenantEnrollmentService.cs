namespace Application.Contracts.Tenancy;

public interface ITenantEnrollmentService
{
    Task EnrollInDefaultTenantAsync(Guid userId, CancellationToken cancellationToken = default);
}
