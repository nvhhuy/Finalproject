using Application.Contracts.Email;
using Application.Contracts.Notifications;
using Application.Contracts.Support;
using Domains.Support;
using Domains.UserAggregate.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Support;

public sealed class SupportTicketService : ISupportTicketService
{
    private readonly ApplicationDbContext _db;
    private readonly IUserNotificationService _notifications;
    private readonly IEmailSender _emailSender;

    public SupportTicketService(
        ApplicationDbContext db,
        IUserNotificationService notifications,
        IEmailSender emailSender)
    {
        _db = db;
        _notifications = notifications;
        _emailSender = emailSender;
    }

    public async Task<Guid> CreateAsync(
        Guid userId,
        string subject,
        string description,
        string? category,
        SupportTicketPriority priority,
        CancellationToken cancellationToken = default)
    {
        int next = await _db.SupportTickets.CountAsync(cancellationToken) + 1;
        string ticketNumber = $"SUP-{DateTime.UtcNow:yyyyMMdd}-{next:D5}";

        SupportTicket ticket = SupportTicket.Create(userId, ticketNumber, subject, description, category, priority);
        _db.SupportTickets.Add(ticket);
        ticket.AddMessage(userId, description, isInternalNote: false);
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAndDispatchAsync(
            userId,
            $"Ticket {ticket.TicketNumber} created",
            "Our support team received your request.",
            $"/app/support/{ticket.Id}",
            cancellationToken);

        UserId userKey = UserId.Create(userId);
        string? email = await _db.Users.Where(u => u.Id == userKey).Select(u => u.Email).FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(email))
        {
            await _emailSender.SendAsync(
                email!,
                $"[{ticket.TicketNumber}] Support ticket created",
                $"<p>We received your request.</p><p><b>{subject}</b></p>",
                cancellationToken);
        }

        return ticket.Id;
    }

    public async Task<bool> AddMessageAsync(
        Guid ticketId,
        Guid actorUserId,
        string body,
        bool isInternalNote,
        IEnumerable<SupportTicketAttachmentDraft>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await _db.SupportTickets
            .Include(t => t.Messages)
            .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
            return false;

        SupportTicketMessage message = ticket.AddMessage(actorUserId, body, isInternalNote, attachments);
        _db.SupportTicketMessages.Add(message);
        if (!isInternalNote && actorUserId != ticket.CreatedByUserId)
            ticket.ChangeStatus(SupportTicketStatus.PendingUser);
        else if (!isInternalNote)
            ticket.ChangeStatus(SupportTicketStatus.PendingSupport);

        await _db.SaveChangesAsync(cancellationToken);

        if (!isInternalNote && actorUserId != ticket.CreatedByUserId)
        {
            await _notifications.CreateAndDispatchAsync(
                ticket.CreatedByUserId,
                $"Update on {ticket.TicketNumber}",
                body,
                $"/app/support/{ticket.Id}",
                cancellationToken);

            UserId ownerKey = UserId.Create(ticket.CreatedByUserId);
            string? email = await _db.Users
                .Where(u => u.Id == ownerKey)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(email))
            {
                await _emailSender.SendAsync(
                    email!,
                    $"[{ticket.TicketNumber}] New support reply",
                    $"<p>{body}</p>",
                    cancellationToken);
            }
        }

        return true;
    }

    public async Task<bool> AssignAsync(Guid ticketId, Guid? adminUserId, CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
            return false;

        ticket.Assign(adminUserId);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ChangeStatusAsync(Guid ticketId, SupportTicketStatus status, CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
            return false;

        ticket.ChangeStatus(status);
        await _db.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAndDispatchAsync(
            ticket.CreatedByUserId,
            $"Ticket {ticket.TicketNumber} status updated",
            $"New status: {status}",
            $"/app/support/{ticket.Id}",
            cancellationToken);
        return true;
    }

    public async Task<bool> ChangePriorityAsync(Guid ticketId, SupportTicketPriority priority, CancellationToken cancellationToken = default)
    {
        SupportTicket? ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
            return false;

        ticket.ChangePriority(priority);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
