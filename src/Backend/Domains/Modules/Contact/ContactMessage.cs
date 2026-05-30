namespace Domains.Contact;

/// <summary>A public contact-form submission stored for admin review.</summary>
public sealed class ContactMessage
{
    private ContactMessage()
    {
    }

    private ContactMessage(Guid id, string name, string email, string subject, string body)
    {
        Id = id;
        Name = name;
        Email = email;
        NormalizedEmail = email.Trim().ToUpperInvariant();
        Subject = subject;
        Body = body;
        IsRead = false;
        SubmittedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public bool IsRead { get; private set; }
    public DateTime SubmittedAtUtc { get; private set; }

    public static ContactMessage Create(string name, string email, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body is required.", nameof(body));
        return new ContactMessage(Guid.NewGuid(), name.Trim(), email.Trim(), subject.Trim(), body.Trim());
    }

    public void MarkRead()
    {
        IsRead = true;
    }
}
