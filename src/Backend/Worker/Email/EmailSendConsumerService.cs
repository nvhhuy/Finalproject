using Application.Contracts.Email;
using Application.Contracts.Messaging;
using Infrastructure.Email;

namespace Worker.Email;

public sealed class EmailSendConsumerService : QueueConsumerBackgroundService<EmailSendMessage>
{
    public const string QueueName = "email.send.v1";
    private readonly ILogger<EmailSendConsumerService> _logger;

    private readonly IServiceScopeFactory _scopeFactory;

    public EmailSendConsumerService(
        IMessageConsumer consumer,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSendConsumerService> logger) : base(consumer, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override string SubscriptionName => QueueName;

    protected override SubscriptionOptions SubscriptionOptions { get; } = new(true);

    protected override async Task HandleAsync(EmailSendMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email worker sending to {To} (event {EventId})", message.To, message.EventId);

        using var scope = _scopeFactory.CreateScope();

        // Resolve SmtpEmailSender directly to bypass OutboxQueuingEmailSender, which is registered
        // as IEmailSender on Api when Email:QueueViaOutbox=true. Using IEmailSender here would cause
        // the consumer to re-queue the message instead of delivering it via SMTP.
        var sender = (IEmailSender?)scope.ServiceProvider.GetService<SmtpEmailSender>()
                     ?? scope.ServiceProvider.GetRequiredService<IEmailSender>();

        await sender.SendAsync(message.To, message.Subject, message.HtmlBody, cancellationToken);
    }
}