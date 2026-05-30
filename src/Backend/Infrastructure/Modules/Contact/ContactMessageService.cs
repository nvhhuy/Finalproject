using Application.Contracts.Contact;
using Domains.Contact;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Contact;

public sealed class ContactMessageService : IContactMessageService
{
    private readonly ApplicationDbContext _db;

    public ContactMessageService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> SubmitAsync(
        string name,
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = ContactMessage.Create(name, email, subject, body);
        _db.ContactMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    public async Task<bool> MarkReadAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _db.ContactMessages
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

        if (message is null) return false;

        message.MarkRead();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
