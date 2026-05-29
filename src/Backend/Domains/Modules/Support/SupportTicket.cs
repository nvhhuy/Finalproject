using Domains.Common;

namespace Domains.Support;

public sealed class SupportTicket : AggregateRoot<Guid>, ITenantScoped
{
    private readonly List<SupportTicketMessage> _messages = [];

    private SupportTicket()
    {
    }

    private SupportTicket(
        Guid id,
        Guid tenantId,
        Guid createdByUserId,
        string ticketNumber,
        string subject,
        string description,
        string? category,
        SupportTicketPriority priority) : base(id)
    {
        TenantId = tenantId;
        CreatedByUserId = createdByUserId;
        TicketNumber = ticketNumber;
        Subject = subject;
        NormalizedSubject = Normalize(subject);
        Description = description;
        Category = category;
        Priority = priority;
        Status = SupportTicketStatus.Open;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
        LastMessageAtUtc = CreatedAtUtc;
        LastMessageByUserId = createdByUserId;
    }

    public Guid CreatedByUserId { get; }
    public string TicketNumber { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string NormalizedSubject { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? Category { get; private set; }
    public SupportTicketPriority Priority { get; private set; }
    public SupportTicketStatus Status { get; private set; }
    public Guid? AssignedAdminUserId { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime LastMessageAtUtc { get; private set; }
    public Guid LastMessageByUserId { get; private set; }
    public DateTime? FirstResponseAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }

    public IReadOnlyList<SupportTicketMessage> Messages => _messages.AsReadOnly();

    public Guid TenantId { get; }

    public static SupportTicket Create(
        Guid tenantId,
        Guid createdByUserId,
        string ticketNumber,
        string subject,
        string description,
        string? category,
        SupportTicketPriority priority)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        return new SupportTicket(Guid.NewGuid(), tenantId, createdByUserId, ticketNumber, subject, description,
            category, priority);
    }

    public SupportTicketMessage AddMessage(
        Guid authorUserId,
        string body,
        bool isInternalNote,
        IEnumerable<SupportTicketAttachmentDraft>? attachments = null)
    {
        var message = SupportTicketMessage.Create(Id, authorUserId, body, isInternalNote);
        if (attachments is not null)
            foreach (var a in attachments)
                message.AddAttachment(a.StorageKey, a.FileName, a.ContentType, a.ByteSize);

        _messages.Add(message);
        UpdatedAtUtc = DateTime.UtcNow;
        LastMessageAtUtc = UpdatedAtUtc;
        LastMessageByUserId = authorUserId;
        if (!isInternalNote && authorUserId != CreatedByUserId)
            FirstResponseAtUtc ??= UpdatedAtUtc;
        return message;
    }

    public void Assign(Guid? adminUserId)
    {
        AssignedAdminUserId = adminUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeStatus(SupportTicketStatus status)
    {
        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
        ClosedAtUtc = status is SupportTicketStatus.Closed ? UpdatedAtUtc : null;
    }

    public void ChangePriority(SupportTicketPriority priority)
    {
        Priority = priority;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}

public sealed record SupportTicketAttachmentDraft(
    string StorageKey,
    string FileName,
    string ContentType,
    long ByteSize);