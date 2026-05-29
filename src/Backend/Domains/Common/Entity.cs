using Domains.Common.Interfaces;

namespace Domains.Common;

public abstract class Entity<TId> : IEquatable<Entity<TId>>, IHasDomainEvents, IHasRowVersion where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected Entity(TId id)
    {
        Id = id;
    }

    protected Entity()
    {
    }

    public TId Id { get; protected set; } = default!;

    public bool Equals(Entity<TId>? other)
    {
        return Equals((object?)other);
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>Optimistic concurrency token; incremented on every successful update.</summary>
    public uint RowVersion { get; private set; }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> entity && Id.Equals(entity.Id);
    }

    public static bool operator ==(Entity<TId> left, Entity<TId> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId> left, Entity<TId> right)
    {
        return !left.Equals(right);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}