using Application.Contracts.Services;
using Application.Contracts.Tenancy;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

public sealed class TenantEnrollmentService : ITenantEnrollmentService
{
    private readonly IDateTimeProvider _clock;
    private readonly ApplicationDbContext _db;

    public TenantEnrollmentService(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnrollInDefaultTenantAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var defaultTenantId = TenantId.Create(SystemTenants.DefaultTenantId);
        var userKey = UserId.Create(userId);

        var tenantExists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == defaultTenantId, cancellationToken);
        if (!tenantExists)
            return;

        var alreadyMember = await _db.TenantMembers.AsNoTracking()
            .AnyAsync(m => m.TenantId == defaultTenantId && m.UserId == userKey, cancellationToken);
        if (alreadyMember)
            return;

        _db.TenantMembers.Add(TenantMember.Create(defaultTenantId, userKey, TenantMemberRoles.Member, _clock.Now));
    }
}