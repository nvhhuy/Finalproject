using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

public sealed class TenantMembershipService : ITenantMembershipService
{
    private readonly ApplicationReadDbContext _db;

    public TenantMembershipService(ApplicationReadDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsMemberAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantKey = TenantId.Create(tenantId);
        var userKey = UserId.Create(userId);
        return await _db.Set<TenantMember>()
            .AsNoTracking()
            .AnyAsync(m => m.TenantId == tenantKey && m.UserId == userKey, cancellationToken);
    }

    public async Task<bool> IsOwnerAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantKey = TenantId.Create(tenantId);
        var userKey = UserId.Create(userId);
        var isOwnerMember = await _db.Set<TenantMember>()
            .AsNoTracking()
            .AnyAsync(
                m => m.TenantId == tenantKey && m.UserId == userKey && m.Role == TenantMemberRoles.Owner,
                cancellationToken);
        if (isOwnerMember)
            return true;

        return await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantKey && t.OwnerUserId == userKey, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetTenantIdsForUserAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userKey = UserId.Create(userId);
        return await _db.Set<TenantMember>()
            .AsNoTracking()
            .Where(m => m.UserId == userKey)
            .Select(m => m.TenantId.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userKey = UserId.Create(userId);
        var role = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userKey)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(cancellationToken);
        return role == UserRoles.Admin;
    }
}