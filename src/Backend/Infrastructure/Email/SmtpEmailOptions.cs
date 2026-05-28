namespace Infrastructure.Email;

/// <summary>SMTP settings for <see cref="SmtpEmailSender"/> (MailKit).</summary>
/// <remarks>
/// <para><b>TLS semantics:</b> the sender uses MailKit's <c>StartTls</c> mode when
/// <see cref="UseSsl"/> is true (the standard for port 587) and <c>Auto</c>-negotiation otherwise.
/// We deliberately do not expose implicit-TLS mode (port 465) — modern relays support STARTTLS
/// on 587 and the implicit-TLS port has been deprecated since RFC 8314.</para>
///
/// <para>Set <see cref="UseSsl"/> to <c>true</c> for every internet-facing relay (Gmail, SES,
/// Mailgun, SendGrid). The dev compose stack runs MailHog on port 1025 without TLS — set this
/// to <c>false</c> only for that exact setup.</para>
/// </remarks>
public sealed class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;

    /// <summary>
    /// When <c>true</c>: connect, then upgrade to TLS via STARTTLS (port 587 convention).
    /// When <c>false</c>: auto-negotiate — used by the local MailHog container on port 1025.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@localhost";
    public string FromDisplayName { get; set; } = "Base Project";
}
