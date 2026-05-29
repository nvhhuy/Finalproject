namespace Domains.Common;

/// <summary>Marks entities that belong to a single tenant and participate in global query filters.</summary>
public interface ITenantScoped
{
    Guid TenantId { get; }
}