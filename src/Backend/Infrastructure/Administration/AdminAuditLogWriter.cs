using Application.Contracts.Administration;
using Application.Contracts.Services;
using Domains.Administration;
using Infrastructure.Persistence;

namespace Infrastructure.Administration;

public sealed class AdminAuditLogWriter : IAdminAuditLogWriter
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AdminAuditLogWriter(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Append(Guid actorUserId, string action, Guid? targetUserId, string? ipAddress, string? details)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            IpAddress = ipAddress,
            Details = details,
            CreatedAtUtc = _clock.Now
        });
    }
}
