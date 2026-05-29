namespace Application.Contracts.Billing;

public interface ITenantRateLimitProvider
{
    int GetRequestsPerMinute(Guid? tenantId);
}