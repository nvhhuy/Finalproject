using Domains.Common.Interfaces;

namespace Domains.Modules.Books.BookAggregate;

public record BookId : IValueObject
{
    private BookId()
    {
    }

    private BookId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; private set; }

    public static BookId Create(Guid value)
    {
        return new BookId(value);
    }

    public static BookId CreateNew()
    {
        return new BookId(Guid.NewGuid());
    }
}
