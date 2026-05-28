using Domains.Support;

namespace Application.Contracts.Support;

public interface ISupportTicketService
{
    Task<Guid> CreateAsync(
        Guid userId,
        string subject,
        string description,
        string? category,
        SupportTicketPriority priority,
        CancellationToken cancellationToken = default);

    Task<bool> AddMessageAsync(
        Guid ticketId,
        Guid actorUserId,
        string body,
        bool isInternalNote,
        IEnumerable<SupportTicketAttachmentDraft>? attachments = null,
        CancellationToken cancellationToken = default);

    Task<bool> AssignAsync(Guid ticketId, Guid? adminUserId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStatusAsync(Guid ticketId, SupportTicketStatus status, CancellationToken cancellationToken = default);
    Task<bool> ChangePriorityAsync(Guid ticketId, SupportTicketPriority priority, CancellationToken cancellationToken = default);
}
