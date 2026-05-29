using Domains.UserAggregate.ValueObjects;

namespace Domains.UserAggregate;

/// <summary>Links a user to an external identity provider (Google, Facebook, …).</summary>
public sealed class ExternalUserLogin
{
    private ExternalUserLogin()
    {
    }

    public Guid Id { get; private set; }
    public UserId UserId { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string ProviderSubject { get; private set; } = null!;

    public static ExternalUserLogin Create(UserId userId, string provider, string providerSubject)
    {
        return new ExternalUserLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider,
            ProviderSubject = providerSubject
        };
    }
}