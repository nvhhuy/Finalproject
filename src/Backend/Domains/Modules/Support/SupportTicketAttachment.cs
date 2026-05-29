using Domains.Common;

namespace Domains.Support;

public sealed class SupportTicketAttachment : Entity<Guid>
{
    private SupportTicketAttachment()
    {
    }

    private SupportTicketAttachment(
        Guid id,
        Guid ticketMessageId,
        string storageKey,
        string fileName,
        string contentType,
        long byteSize) : base(id)
    {
        TicketMessageId = ticketMessageId;
        StorageKey = storageKey;
        FileName = fileName;
        ContentType = contentType;
        ByteSize = byteSize;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid TicketMessageId { get; private set; }
    public string StorageKey { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long ByteSize { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static SupportTicketAttachment Create(
        Guid ticketMessageId,
        string storageKey,
        string fileName,
        string contentType,
        long byteSize)
    {
        return new SupportTicketAttachment(Guid.NewGuid(), ticketMessageId, storageKey, fileName, contentType,
            byteSize);
    }
}