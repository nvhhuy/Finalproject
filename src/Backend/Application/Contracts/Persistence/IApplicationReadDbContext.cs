using Domains.Administration;
using Domains.MediaAggregate;
using Domains.Notifications;
using Domains.Support;
using Domains.UserAggregate;
using Microsoft.EntityFrameworkCore;

namespace Application.Contracts.Persistence;

/// <summary>
/// Read-only PostgreSQL surface for queries (CQRS read side). Spans every aggregate exposed to
/// query handlers — users, media, audit logs — so this lives in a module-agnostic namespace
/// rather than under any one feature module.
/// </summary>
public interface IApplicationReadDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<ExternalUserLogin> ExternalLogins { get; }
    DbSet<AdminAuditLog> AdminAuditLogs { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<SupportTicket> SupportTickets { get; }
    DbSet<SupportTicketMessage> SupportTicketMessages { get; }
    DbSet<SupportTicketAttachment> SupportTicketAttachments { get; }
}
