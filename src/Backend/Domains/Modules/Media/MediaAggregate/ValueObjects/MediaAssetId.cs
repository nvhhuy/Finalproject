using Domains.Common.Interfaces;

namespace Domains.MediaAggregate.ValueObjects;

public record MediaAssetId : IValueObject
{
    private MediaAssetId()
    {
    }

    private MediaAssetId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; private set; }

    public static MediaAssetId Create(Guid value)
    {
        return new MediaAssetId(value);
    }

    public static MediaAssetId CreateNew()
    {
        return new MediaAssetId(Guid.NewGuid());
    }
}