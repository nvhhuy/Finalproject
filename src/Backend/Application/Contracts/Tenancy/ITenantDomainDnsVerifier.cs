namespace Application.Contracts.Tenancy;

public interface ITenantDomainDnsVerifier
{
    Task<bool> HasVerificationTxtRecordAsync(
        string hostname,
        string verificationToken,
        CancellationToken cancellationToken = default);
}