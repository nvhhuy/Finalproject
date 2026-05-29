using Application.Contracts.Tenancy;
using Application.Contracts.Services;
using Domains.Tenancy;
using Domains.Tenancy.ValueObjects;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tenancy;

public sealed class TenantEnrollmentService : ITenantEnrollmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public TenantEnrollmentService(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task EnrollInDefaultTenantAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        TenantId defaultTenantId = TenantId.Create(SystemTenants.DefaultTenantId);
        UserId userKey = UserId.Create(userId);

        bool tenantExists = await _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == defaultTenantId, cancellationToken);
        if (!tenantExists)
            return;

        bool alreadyMember = await _db.TenantMembers.AsNoTracking()
            .AnyAsync(m => m.TenantId == defaultTenantId && m.UserId == userKey, cancellationToken);
        if (alreadyMember)
            return;

        _db.TenantMembers.Add(TenantMember.Create(defaultTenantId, userKey, TenantMemberRoles.Member, _clock.Now));
    }
}
