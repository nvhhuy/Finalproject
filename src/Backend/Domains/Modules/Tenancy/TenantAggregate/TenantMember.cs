using Domains.Common;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;

namespace Domains.Tenancy;

public sealed class TenantMember : Entity<Guid>
{
    private TenantMember()
    {
    }

    private TenantMember(Guid id, TenantId tenantId, UserId userId, string role, DateTime joinedAtUtc) : base(id)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public TenantId TenantId { get; private set; } = null!;
    public UserId UserId { get; private set; } = null!;
    public string Role { get; private set; } = TenantMemberRoles.Member;
    public DateTime JoinedAtUtc { get; private set; }

    public static TenantMember Create(TenantId tenantId, UserId userId, string role, DateTime joinedAtUtc)
    {
        return new TenantMember(Guid.NewGuid(), tenantId, userId, role, joinedAtUtc);
    }
}