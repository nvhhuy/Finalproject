using Domains.Common.Interfaces;

namespace Domains.UserAggregate.ValueObjects;

public record UserId : IValueObject
{
    private UserId()
    {
    } // for ef core

    private UserId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; private set; }

    public static UserId Create(Guid value)
    {
        return new UserId(value);
    }

    public static UserId CreateNew()
    {
        return new UserId(Guid.NewGuid());
    }
}