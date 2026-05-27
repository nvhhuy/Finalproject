namespace Domains.Common;

/// <summary>Marks an aggregate that participates in optimistic concurrency checks.</summary>
public interface IHasRowVersion
{
    uint RowVersion { get; }
}
