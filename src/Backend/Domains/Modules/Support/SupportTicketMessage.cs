using Domains.Common;

namespace Domains.Support;

public sealed class SupportTicketMessage : Entity<Guid>
{
    private readonly List<SupportTicketAttachment> _attachments = [];

    private SupportTicketMessage()
    {
    }

    private SupportTicketMessage(Guid id, Guid ticketId, Guid authorUserId, string body, bool isInternalNote)
        : base(id)
    {
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        Body = body;
        IsInternalNote = isInternalNote;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = null!;
    public bool IsInternalNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyList<SupportTicketAttachment> Attachments => _attachments.AsReadOnly();

    public static SupportTicketMessage Create(Guid ticketId, Guid authorUserId, string body, bool isInternalNote)
    {
        return new SupportTicketMessage(Guid.NewGuid(), ticketId, authorUserId, body, isInternalNote);
    }

    public void AddAttachment(string storageKey, string fileName, string contentType, long byteSize)
    {
        _attachments.Add(SupportTicketAttachment.Create(Id, storageKey, fileName, contentType, byteSize));
    }
}