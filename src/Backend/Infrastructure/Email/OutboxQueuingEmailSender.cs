using Application.Contracts.Email;
using Application.Contracts.Messaging;
using Domains.Email.Events;

namespace Infrastructure.Email;

/// <summary>Queues email via transactional outbox; Worker sends through SMTP.</summary>
public sealed class OutboxQueuingEmailSender : IEmailSender
{
    private readonly IIntegrationEventOutboxWriter _outbox;

    public OutboxQueuingEmailSender(IIntegrationEventOutboxWriter outbox) => _outbox = outbox;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var evt = new SendEmailIntegrationEvent(to, subject, htmlBody);
        return _outbox.EnqueueAsync(evt, cancellationToken);
    }
}
