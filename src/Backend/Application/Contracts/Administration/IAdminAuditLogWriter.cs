namespace Application.Contracts.Administration;

/// <summary>Queues an admin audit row on the current write DbContext; persists with the next SaveChanges.</summary>
public interface IAdminAuditLogWriter
{
    void Append(Guid actorUserId, string action, Guid? targetUserId, string? ipAddress, string? details);
}