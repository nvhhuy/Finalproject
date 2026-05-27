namespace Domains.Common;

/// <summary>
/// Optimistic concurrency marker. Implemented by <see cref="Entity{TId}"/> so every domain entity
/// inherits a <c>RowVersion</c> column without per-type boilerplate.
/// </summary>
public interface IHasRowVersion
{
    uint RowVersion { get; }
}
