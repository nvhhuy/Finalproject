using Domains.Common.Interfaces;

namespace Domains.UserAggregate.ValueObjects
{
    public record UserId : IValueObject
    {
        public Guid Value { get; private set; }
        private UserId() { } // for ef core
        private UserId(Guid value)
        {
            Value = value;
        }
        public static UserId Create(Guid value) => new(value);
        public static UserId CreateNew() => new(Guid.NewGuid());
    }
}
