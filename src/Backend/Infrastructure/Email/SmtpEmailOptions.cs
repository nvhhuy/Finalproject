namespace Infrastructure.Email;

/// <summary>SMTP settings for <see cref="SmtpEmailSender"/> (MailKit).</summary>
public sealed class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@localhost";
    public string FromDisplayName { get; set; } = "Final Project";
}
