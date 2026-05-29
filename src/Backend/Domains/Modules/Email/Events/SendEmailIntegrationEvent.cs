using Domains.Common.Interfaces;

namespace Domains.Email.Events;

/// <summary>Queued email delivery via transactional outbox and Worker consumer.</summary>
public sealed record SendEmailIntegrationEvent : IIntegrationEvent
{
    public SendEmailIntegrationEvent(string to, string subject, string htmlBody)
    {
        EventId = Guid.NewGuid();
        OccurredOnUtc = DateTime.UtcNow;
        To = to;
        Subject = subject;
        HtmlBody = htmlBody;
    }

    public string To { get; init; } = null!;
    public string Subject { get; init; } = null!;
    public string HtmlBody { get; init; } = null!;

    public Guid EventId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public string RoutingKey => "email.send.v1";
}