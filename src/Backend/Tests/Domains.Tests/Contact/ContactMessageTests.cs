using Domains.Contact;

namespace Domains.Tests.Contact;

/// <summary>
///     Unit tests for the <see cref="ContactMessage" /> entity. Covers factory guards on blank
///     inputs and the state transition from unread to read.
/// </summary>
public sealed class ContactMessageTests
{
    private const string ValidName = "Jane Smith";
    private const string ValidEmail = "jane@example.com";
    private const string ValidSubject = "Question about billing";
    private const string ValidBody = "I have a question about my last invoice.";

    [Fact]
    public void Create_throws_when_name_is_empty()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContactMessage.Create(string.Empty, ValidEmail, ValidSubject, ValidBody));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Create_throws_when_name_is_whitespace()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContactMessage.Create("   ", ValidEmail, ValidSubject, ValidBody));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Create_throws_when_email_is_empty()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContactMessage.Create(ValidName, string.Empty, ValidSubject, ValidBody));

        Assert.Equal("email", ex.ParamName);
    }

    [Fact]
    public void Create_throws_when_subject_is_empty()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContactMessage.Create(ValidName, ValidEmail, string.Empty, ValidBody));

        Assert.Equal("subject", ex.ParamName);
    }

    [Fact]
    public void Create_throws_when_body_is_empty()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ContactMessage.Create(ValidName, ValidEmail, ValidSubject, string.Empty));

        Assert.Equal("body", ex.ParamName);
    }

    [Fact]
    public void Create_returns_unread_message_with_correct_fields()
    {
        var before = DateTime.UtcNow;
        var msg = ContactMessage.Create(ValidName, ValidEmail, ValidSubject, ValidBody);
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, msg.Id);
        Assert.Equal(ValidName, msg.Name);
        Assert.Equal(ValidEmail, msg.Email);
        Assert.Equal(ValidEmail.ToUpperInvariant(), msg.NormalizedEmail);
        Assert.Equal(ValidSubject, msg.Subject);
        Assert.Equal(ValidBody, msg.Body);
        Assert.False(msg.IsRead);
        Assert.InRange(msg.SubmittedAtUtc, before, after);
    }

    [Fact]
    public void Create_trims_whitespace_from_fields()
    {
        var msg = ContactMessage.Create(
            "  Jane Smith  ",
            "  jane@example.com  ",
            "  Billing question  ",
            "  Some body text  ");

        Assert.Equal("Jane Smith", msg.Name);
        Assert.Equal("jane@example.com", msg.Email);
        Assert.Equal("Billing question", msg.Subject);
        Assert.Equal("Some body text", msg.Body);
    }

    [Fact]
    public void MarkRead_sets_IsRead_to_true()
    {
        var msg = ContactMessage.Create(ValidName, ValidEmail, ValidSubject, ValidBody);
        Assert.False(msg.IsRead);

        msg.MarkRead();

        Assert.True(msg.IsRead);
    }

    [Fact]
    public void MarkRead_is_idempotent()
    {
        var msg = ContactMessage.Create(ValidName, ValidEmail, ValidSubject, ValidBody);

        msg.MarkRead();
        msg.MarkRead(); // second call must not throw

        Assert.True(msg.IsRead);
    }

    [Fact]
    public void NormalizedEmail_is_uppercase()
    {
        var msg = ContactMessage.Create(ValidName, "Mixed.Case@Example.COM", ValidSubject, ValidBody);

        Assert.Equal("MIXED.CASE@EXAMPLE.COM", msg.NormalizedEmail);
    }
}
