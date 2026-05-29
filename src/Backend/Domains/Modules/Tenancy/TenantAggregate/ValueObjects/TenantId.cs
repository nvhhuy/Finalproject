using Domains.Common.Interfaces;

namespace Domains.Tenancy.ValueObjects;

public sealed record TenantId : IValueObject
{
    private TenantId()
    {
    }

    private TenantId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; private set; }

    public static TenantId Create(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Tenant id cannot be empty.", nameof(value));
        return new TenantId(value);
    }

    public static TenantId CreateNew()
    {
        return new TenantId(Guid.NewGuid());
    }
}