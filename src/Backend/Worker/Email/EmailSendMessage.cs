namespace Worker.Email;

/// <summary>Wire shape for <c>email.send.v1</c> (mirrors <see cref="Domains.Email.Events.SendEmailIntegrationEvent"/> JSON).</summary>
public sealed record EmailSendMessage(
    Guid EventId,
    DateTime OccurredOnUtc,
    string To,
    string Subject,
    string HtmlBody);
