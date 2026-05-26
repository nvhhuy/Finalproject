using Domains.Common.Interfaces;

namespace Domains.MediaAggregate.ValueObjects;

public record MediaAssetId : IValueObject
{
    public Guid Value { get; private set; }
    private MediaAssetId() { }
    private MediaAssetId(Guid value) { Value = value; }
    public static MediaAssetId Create(Guid value) => new(value);
    public static MediaAssetId CreateNew() => new(Guid.NewGuid());
}
