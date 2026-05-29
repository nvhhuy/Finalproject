namespace Domains.Tenancy;

public enum TenantDomainStatus
{
    PendingVerification = 0,
    Verified = 1,
    Active = 2,
    Disabled = 3
}