using Application.Contracts.Email;
using Application.Contracts.Messaging;

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
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await sender.SendAsync(message.To, message.Subject, message.HtmlBody, cancellationToken);
    }
}